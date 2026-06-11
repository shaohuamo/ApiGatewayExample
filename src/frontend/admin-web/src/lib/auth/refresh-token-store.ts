import { createCipheriv, createDecipheriv, createHash, randomBytes, randomUUID } from "crypto";

type PgModule = typeof import("pg");
type Pool = import("pg").Pool;

type RefreshTokenRecord = {
  id: string;
  refresh_token: string;
};

const ENCRYPTION_ALGORITHM = "aes-256-gcm";
const ENCRYPTION_VERSION = "v1";
const DEFAULT_MAX_DB_RETRIES = 5;
const DEFAULT_INITIAL_RETRY_DELAY_MS = 200;
const DEFAULT_MAX_RETRY_DELAY_MS = 3_000;
const TRANSIENT_POSTGRES_ERROR_CODES = new Set([
  "40001", // serialization_failure
  "40P01", // deadlock_detected
  "53300", // too_many_connections
  "53400", // configuration_limit_exceeded
  "57P01", // admin_shutdown
  "57P02", // crash_shutdown
  "57P03", // cannot_connect_now
  "58000", // system_error
  "58030", // io_error
]);
const TRANSIENT_NODE_ERROR_CODES = new Set([
  "ECONNREFUSED",
  "ECONNRESET",
  "EHOSTUNREACH",
  "ENETDOWN",
  "ENETUNREACH",
  "ETIMEDOUT",
]);

declare global {
  // eslint-disable-next-line no-var
  var adminWebRefreshTokenPool: Pool | undefined;
  // eslint-disable-next-line no-var
  var adminWebRefreshTokenSchemaReady: Promise<void> | undefined;
}

function getConnectionString() {
  if (process.env.AUTH_POSTGRES_CONNECTION_STRING) {
    return process.env.AUTH_POSTGRES_CONNECTION_STRING;
  }

  const host = process.env.AUTH_POSTGRES_HOST || "localhost";
  const port = process.env.AUTH_POSTGRES_PORT || "5432";
  const database = process.env.AUTH_POSTGRES_DATABASE || "adminwebdatabase";
  const user = process.env.AUTH_POSTGRES_USER || "postgres";
  const password = process.env.AUTH_POSTGRES_PASSWORD || "admin";

  return `postgres://${encodeURIComponent(user)}:${encodeURIComponent(password)}@${host}:${port}/${database}`;
}

function getNumberOption(name: string, fallback: number) {
  const value = process.env[name];
  const parsed = value ? Number(value) : NaN;

  return Number.isFinite(parsed) && parsed >= 0
    ? parsed
    : fallback;
}

function getEncryptionKey() {
  const secret = process.env.AUTH_SECRET;

  if (!secret) {
    throw new Error("AUTH_SECRET is required to encrypt refresh tokens.");
  }

  return createHash("sha256").update(secret).digest();
}

function encryptRefreshToken(refreshToken: string) {
  const iv = randomBytes(12);
  const cipher = createCipheriv(ENCRYPTION_ALGORITHM, getEncryptionKey(), iv);
  const encrypted = Buffer.concat([
    cipher.update(refreshToken, "utf8"),
    cipher.final(),
  ]);
  const authTag = cipher.getAuthTag();

  return [
    ENCRYPTION_VERSION,
    iv.toString("base64url"),
    authTag.toString("base64url"),
    encrypted.toString("base64url"),
  ].join(":");
}

function decryptRefreshToken(encryptedRefreshToken: string) {
  const [version, iv, authTag, encrypted] = encryptedRefreshToken.split(":");

  if (version !== ENCRYPTION_VERSION || !iv || !authTag || !encrypted) {
    throw new Error("Unsupported refresh token encryption format.");
  }

  const decipher = createDecipheriv(
    ENCRYPTION_ALGORITHM,
    getEncryptionKey(),
    Buffer.from(iv, "base64url"),
  );
  decipher.setAuthTag(Buffer.from(authTag, "base64url"));

  return Buffer.concat([
    decipher.update(Buffer.from(encrypted, "base64url")),
    decipher.final(),
  ]).toString("utf8");
}

function delay(ms: number) {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

function computeRetryDelay(attempt: number) {
  const initialRetryDelayMs = getNumberOption(
    "AUTH_POSTGRES_INITIAL_RETRY_DELAY_MS",
    DEFAULT_INITIAL_RETRY_DELAY_MS,
  );
  const maxRetryDelayMs = getNumberOption(
    "AUTH_POSTGRES_MAX_RETRY_DELAY_MS",
    DEFAULT_MAX_RETRY_DELAY_MS,
  );
  const exponential = Math.min(
    initialRetryDelayMs * Math.pow(2, attempt),
    maxRetryDelayMs,
  );

  return exponential * (0.5 + Math.random() * 0.5);
}

function isTransientDbError(error: unknown) {
  if (!(error instanceof Error)) {
    return false;
  }

  const errorCode = "code" in error && typeof error.code === "string"
    ? error.code
    : undefined;

  if (!errorCode) {
    return false;
  }

  return TRANSIENT_POSTGRES_ERROR_CODES.has(errorCode)
    || TRANSIENT_NODE_ERROR_CODES.has(errorCode);
}

async function executeWithRetry<T>(operation: () => Promise<T>) {
  const maxDbRetries = getNumberOption("AUTH_POSTGRES_MAX_RETRIES", DEFAULT_MAX_DB_RETRIES);

  for (let attempt = 0; ; attempt++) {
    try {
      return await operation();
    } catch (error) {
      if (attempt >= maxDbRetries || !isTransientDbError(error)) {
        throw error;
      }

      await delay(computeRetryDelay(attempt));
    }
  }
}

async function getPool() {
  if (!globalThis.adminWebRefreshTokenPool) {
    const { Pool } = await import("pg") as PgModule;
    globalThis.adminWebRefreshTokenPool = new Pool({
      connectionString: getConnectionString(),
    });
  }

  return globalThis.adminWebRefreshTokenPool;
}

async function ensureSchema() {
  globalThis.adminWebRefreshTokenSchemaReady ??= (async () => {
    const pool = await getPool();
    await executeWithRetry(() =>
      pool.query(`
        CREATE TABLE IF NOT EXISTS auth_refresh_tokens (
          id uuid PRIMARY KEY,
          user_id text NOT NULL,
          refresh_token text NOT NULL,
          created_at timestamptz NOT NULL DEFAULT now(),
          updated_at timestamptz NOT NULL DEFAULT now()
        );

        CREATE INDEX IF NOT EXISTS ix_auth_refresh_tokens_user_id
          ON auth_refresh_tokens (user_id);
      `),
    );
  })();

  try {
    return await globalThis.adminWebRefreshTokenSchemaReady;
  } catch (error) {
    globalThis.adminWebRefreshTokenSchemaReady = undefined;
    throw error;
  }
}

export async function createRefreshTokenRecord(userId: string, refreshToken: string) {
  await ensureSchema();

  const id = randomUUID();
  const pool = await getPool();

  await executeWithRetry(() =>
    pool.query(
      `
        INSERT INTO auth_refresh_tokens (id, user_id, refresh_token)
        VALUES ($1, $2, $3);
      `,
      [id, userId, encryptRefreshToken(refreshToken)],
    ),
  );

  return id;
}

export async function getRefreshTokenRecord(id: string): Promise<RefreshTokenRecord | null> {
  await ensureSchema();

  const pool = await getPool();
  const result = await executeWithRetry(() =>
    pool.query<RefreshTokenRecord>(
      `
        SELECT id, refresh_token
        FROM auth_refresh_tokens
        WHERE id = $1;
      `,
      [id],
    ),
  );

  const record = result.rows[0];

  if (!record) {
    return null;
  }

  return {
    id: record.id,
    refresh_token: decryptRefreshToken(record.refresh_token),
  };
}

export async function updateRefreshTokenRecord(id: string, refreshToken: string) {
  await ensureSchema();

  const pool = await getPool();
  await executeWithRetry(() =>
    pool.query(
      `
        UPDATE auth_refresh_tokens
        SET refresh_token = $2,
            updated_at = now()
        WHERE id = $1;
      `,
      [id, encryptRefreshToken(refreshToken)],
    ),
  );
}

export async function deleteRefreshTokenRecord(id: string) {
  await ensureSchema();

  const pool = await getPool();
  await executeWithRetry(() =>
    pool.query(
      `
        DELETE FROM auth_refresh_tokens
        WHERE id = $1;
      `,
      [id],
    ),
  );
}
