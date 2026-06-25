import { cookies, headers } from "next/headers";
import {
  CULTURE_COOKIE_NAME,
  DEFAULT_LOCALE,
  parseCultureCookie,
  resolveLocaleFromAcceptLanguage,
  type Locale,
} from "@/lib/i18n/config";

export async function getRequestLocale(): Promise<Locale> {
  const cookieStore = await cookies();
  const localeFromCookie = parseCultureCookie(cookieStore.get(CULTURE_COOKIE_NAME)?.value);

  if (localeFromCookie) return localeFromCookie;

  const headerStore = await headers();
  const acceptLanguage = headerStore.get("accept-language");

  return acceptLanguage
    ? resolveLocaleFromAcceptLanguage(acceptLanguage)
    : DEFAULT_LOCALE;
}
