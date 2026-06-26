import { afterEach, describe, expect, it, vi } from "vitest";
import { GET } from "@/app/api/culture/route";
import { CULTURE_COOKIE_NAME } from "@/lib/i18n/config";

function getSetCookieHeaders(response: Response) {
  const headers = response.headers as Headers & { getSetCookie?: () => string[] };

  if (headers.getSetCookie) {
    return headers.getSetCookie();
  }

  return headers.get("set-cookie")?.split(/,(?=\s*MicroservicesDemo\.Culture=)/) ?? [];
}

describe("GET /api/culture", () => {
  afterEach(() => {
    vi.unstubAllEnvs();
  });

  it("clears stale host-only and parent-domain culture cookies before setting the canonical cookie", () => {
    vi.stubEnv("FRONTEND_PUBLIC_URL", "https://250669.xyz");

    const response = GET(new Request("http://admin-web-service/api/culture?culture=zh-CN&returnUrl=/products"));
    const setCookieHeaders = getSetCookieHeaders(response);

    expect(setCookieHeaders).toHaveLength(3);
    expect(setCookieHeaders[0]).toContain(`${CULTURE_COOKIE_NAME}=`);
    expect(setCookieHeaders[0]).toContain("Max-Age=0");
    expect(setCookieHeaders[0]).not.toContain("Domain=");

    expect(setCookieHeaders[1]).toContain(`${CULTURE_COOKIE_NAME}=`);
    expect(setCookieHeaders[1]).toContain("Max-Age=0");
    expect(setCookieHeaders[1]).toContain("Domain=.250669.xyz");

    expect(setCookieHeaders[2]).toContain(`${CULTURE_COOKIE_NAME}=c%3Dzh-CN%7Cuic%3Dzh-CN`);
    expect(setCookieHeaders[2]).toContain("Domain=.250669.xyz");
    expect(setCookieHeaders[2]).toContain("Path=/");
    expect(setCookieHeaders[2]).toContain("SameSite=Lax");
    expect(setCookieHeaders[2]).toContain("Secure");
  });
});
