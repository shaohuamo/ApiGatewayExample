import NextAuth, { customFetch } from "next-auth";
import type { OIDCConfig } from "@auth/core/providers";
import type { Profile } from "next-auth";
import { denylistAccessToken } from "@/lib/auth/access-token-denylist";
import {
  createRefreshTokenRecord,
  deleteRefreshTokenRecord,
  getRefreshTokenRecord,
  updateRefreshTokenRecord,
} from "@/lib/auth/refresh-token-store";
import {
  getBodyForLog,
  getHeadersForLog,
  getMethodForLog,
  getResponseBodyForLog,
  getUrlForLog,
  logDevelopmentHttp,
} from "@/lib/dev-http-logging";

function trimTrailingSlash(value: string) {
  return value.endsWith("/") ? value.slice(0, -1) : value;
}

function getIdentityServerPublicUrl() {
  return trimTrailingSlash(process.env.IDENTITYSERVER_PUBLIC_URL || "http://localhost:8085");
}

function getIdentityServerInternalUrl() {
  return trimTrailingSlash(process.env.IDENTITYSERVER_INTERNAL_URL || getIdentityServerPublicUrl());
}

function getFrontendClientId() {
  return process.env.IDENTITYSERVER_FRONTEND_CLIENT_ID || "test_app";
}

function getFrontendClientSecret() {
  return process.env.IDENTITYSERVER_FRONTEND_CLIENT_SECRET || "frontend-secret";
}

function rewriteIdentityServerUrl(input: Parameters<typeof fetch>[0]) {
  const publicUrl = getIdentityServerPublicUrl();
  const internalUrl = getIdentityServerInternalUrl();
  const url = typeof input === "string" || input instanceof URL
    ? new URL(input)
    : new URL(input.url);

  if (url.origin === publicUrl) {
    return `${internalUrl}${url.pathname}${url.search}`;
  }

  return input;
}

function replaceOrigin(value: unknown, from: string, to: string): unknown {
  if (typeof value === "string") {
    return value.startsWith(from) ? `${to}${value.slice(from.length)}` : value;
  }

  if (Array.isArray(value)) {
    return value.map((item) => replaceOrigin(item, from, to));
  }

  if (value && typeof value === "object") {
    return Object.fromEntries(
      Object.entries(value).map(([key, item]) => [key, replaceOrigin(item, from, to)])
    );
  }

  return value;
}

async function fetchIdentityServer(input: Parameters<typeof fetch>[0], init?: Parameters<typeof fetch>[1]) {
  const publicUrl = getIdentityServerPublicUrl();
  const internalUrl = getIdentityServerInternalUrl();
  const rewrittenInput = rewriteIdentityServerUrl(input);

  logDevelopmentHttp("identityserver request", {
    method: getMethodForLog(input, init),
    url: getUrlForLog(input),
    rewrittenUrl: getUrlForLog(rewrittenInput),
    headers: getHeadersForLog(init?.headers),
    body: getBodyForLog(init?.body),
  });

  const response = await fetch(rewrittenInput, init);
  const url = typeof input === "string" || input instanceof URL
    ? new URL(input)
    : new URL(input.url);

  logDevelopmentHttp("identityserver response", {
    method: getMethodForLog(input, init),
    url: getUrlForLog(input),
    rewrittenUrl: getUrlForLog(rewrittenInput),
    status: response.status,
    statusText: response.statusText,
    headers: getHeadersForLog(response.headers),
    body: await getResponseBodyForLog(response),
  });

  if (url.pathname !== "/.well-known/openid-configuration") {
    return response;
  }

  const metadata = await response.json();
  const publicMetadata = replaceOrigin(metadata, internalUrl, publicUrl);

  return new Response(JSON.stringify(publicMetadata), {
    status: response.status,
    statusText: response.statusText,
    headers: {
      "content-type": "application/json",
    },
  });
}

type IdentityServerProfile = Profile & {
  preferred_username?: string;
};

type TokenRefreshResponse = {
  access_token?: string;
  expires_in?: number;
  refresh_token?: string;
  id_token?: string;
  error?: string;
  error_description?: string;
};

const ACCESS_TOKEN_REFRESH_SKEW_SECONDS = 60;

function getTokenEndpoint() {
  return `${getIdentityServerPublicUrl()}/connect/token`;
}

async function refreshAccessToken(refreshToken: string): Promise<TokenRefreshResponse> {
  const requestBody = new URLSearchParams({
    grant_type: "refresh_token",
    refresh_token: refreshToken,
    client_id: getFrontendClientId(),
    client_secret: getFrontendClientSecret(),
  });

  logDevelopmentHttp("identityserver refresh token request", {
    method: "POST",
    url: getTokenEndpoint(),
    rewrittenUrl: getUrlForLog(rewriteIdentityServerUrl(getTokenEndpoint())),
    headers: {
      "content-type": "application/x-www-form-urlencoded",
    },
    body: getBodyForLog(requestBody),
    refreshToken,
  });

  const response = await fetch(rewriteIdentityServerUrl(getTokenEndpoint()), {
    method: "POST",
    headers: {
      "content-type": "application/x-www-form-urlencoded",
    },
    body: requestBody,
  });

  const refreshedToken = await response.json() as TokenRefreshResponse;

  logDevelopmentHttp("identityserver refresh token response", {
    status: response.status,
    statusText: response.statusText,
    accessToken: refreshedToken.access_token,
    refreshToken: refreshedToken.refresh_token,
    idToken: refreshedToken.id_token,
    body: refreshedToken,
  });

  if (!response.ok) {
    throw new Error(refreshedToken.error_description ?? refreshedToken.error ?? "Unable to refresh access token.");
  }

  return refreshedToken;
}

const identityServerProvider: OIDCConfig<IdentityServerProfile> = {
  id: "identity-server",
  name: "IdentityServer",
  type: "oidc",
  issuer: getIdentityServerPublicUrl(),
  wellKnown: `${getIdentityServerPublicUrl()}/.well-known/openid-configuration`,
  clientId: getFrontendClientId(),
  clientSecret: getFrontendClientSecret(),
  authorization: {
    params: {
      scope: "openid profile products-api offline_access",
    },
  },
  checks: ["pkce", "state"],
  profile(profile) {
    const id = profile.sub ?? profile.id ?? undefined;
    const name = profile.preferred_username ?? profile.name ?? profile.sub ?? undefined;

    return {
      id,
      name,
      email: profile.email ?? undefined,
    };
  },
  [customFetch]: fetchIdentityServer,
};

export const { handlers, auth, signIn, signOut } = NextAuth({
  trustHost: true,
  secret: process.env.AUTH_SECRET,
  session: {
    strategy: "jwt",
  },
  providers: [identityServerProvider],
  events: {
    async signOut(message) {
      if (!("token" in message) || !message.token) {
        return;
      }

      const accessToken = typeof message.token.accessToken === "string"
        ? message.token.accessToken
        : undefined;
      const refreshTokenRecordId = typeof message.token.refreshTokenRecordId === "string"
        ? message.token.refreshTokenRecordId
        : undefined;

      await Promise.all([
        accessToken ? denylistAccessToken(accessToken) : Promise.resolve(),
        refreshTokenRecordId ? deleteRefreshTokenRecord(refreshTokenRecordId) : Promise.resolve(),
      ]);
    },
  },
  callbacks: {
    async redirect({ url, baseUrl }) {
      const identityServerPublicUrl = getIdentityServerPublicUrl();

      if (url.startsWith(identityServerPublicUrl)) {
        return url;
      }

      if (url.startsWith("/")) {
        return `${baseUrl}${url}`;
      }

      if (new URL(url).origin === baseUrl) {
        return url;
      }

      return baseUrl;
    },
    // token is Auth.js' internal JWT session payload. It is encrypted/signed into
    // the session cookie. Keep accessToken here, but store refreshToken in PostgreSQL
    // and keep only refreshTokenRecordId in this cookie payload.
    async jwt({ token, account, user }) {
      if (account?.access_token) {
        token.accessToken = account.access_token;
      }

      if (account?.id_token) {
        token.idToken = account.id_token;
      }

      if (account?.refresh_token) {
        const userId = user?.id ?? token.sub;

        if (userId) {
          token.refreshTokenRecordId = await createRefreshTokenRecord(userId, account.refresh_token);
        } else {
          token.error = "RefreshTokenStoreError";
        }
      }

      if (account) {
        logDevelopmentHttp("identityserver sign-in token response", {
          provider: account.provider,
          type: account.type,
          accessToken: account.access_token,
          refreshToken: account.refresh_token,
          idToken: account.id_token,
          expiresAt: account.expires_at,
          expiresIn: account.expires_in,
          scope: account.scope,
          tokenType: account.token_type,
        });
      }

      if (account?.expires_at) {
        token.accessTokenExpiresAt = account.expires_at;
      } else if (account?.expires_in) {
        token.accessTokenExpiresAt = Math.floor(Date.now() / 1000) + account.expires_in;
      }

      token.error = undefined;

      const accessTokenExpiresAt = typeof token.accessTokenExpiresAt === "number"
        ? token.accessTokenExpiresAt
        : undefined;

      if (!accessTokenExpiresAt) {
        return token;
      }

      const shouldRefreshAccessToken =
        Date.now() >= (accessTokenExpiresAt - ACCESS_TOKEN_REFRESH_SKEW_SECONDS) * 1000;

      if (!shouldRefreshAccessToken) {
        return token;
      }

      const refreshTokenRecordId = typeof token.refreshTokenRecordId === "string"
        ? token.refreshTokenRecordId
        : undefined;

      if (!refreshTokenRecordId) {
        token.error = "RefreshTokenMissing";
        return token;
      }

      try {
        const refreshTokenRecord = await getRefreshTokenRecord(refreshTokenRecordId);

        if (!refreshTokenRecord) {
          token.error = "RefreshTokenMissing";
          return token;
        }

        const refreshedToken = await refreshAccessToken(refreshTokenRecord.refresh_token);

        if (!refreshedToken.access_token) {
          throw new Error("Token refresh response did not include an access token.");
        }

        token.accessToken = refreshedToken.access_token;
        token.idToken = refreshedToken.id_token ?? token.idToken;
        token.accessTokenExpiresAt = Math.floor(Date.now() / 1000) + (refreshedToken.expires_in ?? 0);

        if (refreshedToken.refresh_token) {
          await updateRefreshTokenRecord(refreshTokenRecordId, refreshedToken.refresh_token);
        }

        return token;
      } catch {
        token.error = "RefreshAccessTokenError";
        return token;
      }
    },
    // session is the application-facing object returned by auth()/useSession().
    // Only copy fields that the app needs; do not expose refreshToken here.
    session({ session, token }) {
      session.accessToken = typeof token.accessToken === "string" ? token.accessToken : undefined;
      session.idToken = typeof token.idToken === "string" ? token.idToken : undefined;
      session.error = token.error === "RefreshTokenMissing"
        || token.error === "RefreshAccessTokenError"
        || token.error === "RefreshTokenStoreError"
        ? token.error
        : undefined;
      return session;
    },
  },
});
