type RedisModule = typeof import("redis");
type RedisClient = {
  connect: () => Promise<unknown>;
  on: (event: "error", listener: (error: unknown) => void) => unknown;
  set: (key: string, value: string, options: { EX: number }) => Promise<unknown>;
};

declare global {
  // eslint-disable-next-line no-var
  var adminWebAccessTokenDenylistRedis: RedisClient | undefined;
}

type JwtPayload = {
  jti?: string;
  exp?: number;
};

function getRedisUrl() {
  return process.env.AUTH_REDIS_URL || "redis://localhost:6379";
}

function getDenylistKey(jti: string) {
  const prefix = process.env.AUTH_ACCESS_TOKEN_DENYLIST_PREFIX || "admin-web:access-token-denylist";
  return `${prefix}:${jti}`;
}

async function getRedisClient() {
  if (!globalThis.adminWebAccessTokenDenylistRedis) {
    const { createClient } = await import("redis") as RedisModule;
    const client = createClient({
      url: getRedisUrl(),
    });

    client.on("error", () => {
      // Redis command failures are surfaced through rejected promises below.
    });

    await client.connect();
    globalThis.adminWebAccessTokenDenylistRedis = client;
  }

  return globalThis.adminWebAccessTokenDenylistRedis;
}

function base64UrlDecode(value: string) {
  const base64 = value.replace(/-/g, "+").replace(/_/g, "/");
  const padded = base64.padEnd(base64.length + (4 - base64.length % 4) % 4, "=");

  return Buffer.from(padded, "base64").toString("utf8");
}

function decodeJwtPayload(accessToken: string): JwtPayload | null {
  const [, payload] = accessToken.split(".");

  if (!payload) {
    return null;
  }

  try {
    return JSON.parse(base64UrlDecode(payload)) as JwtPayload;
  } catch {
    return null;
  }
}

export async function denylistAccessToken(accessToken: string) {
  const payload = decodeJwtPayload(accessToken);
  const now = Math.floor(Date.now() / 1000);

  if (!payload?.jti || !payload.exp || payload.exp <= now) {
    return;
  }

  const ttlSeconds = payload.exp - now;
  const redis = await getRedisClient();

  await redis.set(getDenylistKey(payload.jti), "1", {
    EX: ttlSeconds,
  });
}
