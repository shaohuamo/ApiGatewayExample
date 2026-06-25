export const SUPPORTED_LOCALES = ["en", "zh-CN"] as const;
export type Locale = (typeof SUPPORTED_LOCALES)[number];

export const DEFAULT_LOCALE: Locale = "en";
export const CULTURE_COOKIE_NAME = "MicroservicesDemo.Culture";

export function isSupportedLocale(value: string | null | undefined): value is Locale {
  return SUPPORTED_LOCALES.includes(value as Locale);
}

export function normalizeLocale(value: string | null | undefined): Locale | null {
  if (!value) return null;
  const normalized = value.toLowerCase();

  if (normalized === "zh" || normalized === "zh-cn" || normalized.startsWith("zh-")) {
    return "zh-CN";
  }

  if (normalized === "en" || normalized.startsWith("en-")) {
    return "en";
  }

  return isSupportedLocale(value) ? value : null;
}

export function parseCultureCookie(value: string | undefined): Locale | null {
  if (!value) return null;

  const culture = value
    .split("|")
    .map((part) => part.trim())
    .find((part) => part.startsWith("c="))
    ?.slice(2);

  return normalizeLocale(culture);
}

export function formatCultureCookie(locale: Locale) {
  return `c=${locale}|uic=${locale}`;
}

export function resolveLocaleFromAcceptLanguage(value: string | null | undefined): Locale {
  if (!value) return DEFAULT_LOCALE;

  const candidates = value
    .split(",")
    .map((part) => part.split(";")[0]?.trim())
    .filter(Boolean);

  for (const candidate of candidates) {
    const locale = normalizeLocale(candidate);
    if (locale) return locale;
  }

  return DEFAULT_LOCALE;
}

export function getCultureCookieDomain(hostname: string) {
  const configuredDomain = process.env.CULTURE_COOKIE_DOMAIN;
  if (configuredDomain) return configuredDomain;

  return hostname === "250669.xyz" || hostname.endsWith(".250669.xyz")
    ? ".250669.xyz"
    : undefined;
}
