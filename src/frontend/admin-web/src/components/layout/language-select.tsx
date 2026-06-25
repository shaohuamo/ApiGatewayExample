"use client";

import { usePathname, useSearchParams } from "next/navigation";
import { SUPPORTED_LOCALES, type Locale } from "@/lib/i18n/config";
import { useI18n } from "@/lib/i18n/provider";

export function LanguageSelect() {
  const pathname = usePathname();
  const searchParams = useSearchParams();
  const { locale, t } = useI18n();

  function handleChange(nextLocale: Locale) {
    const currentUrl = `${pathname}${searchParams.size ? `?${searchParams.toString()}` : ""}`;
    const params = new URLSearchParams({
      culture: nextLocale,
      returnUrl: currentUrl,
    });

    window.location.href = `/api/culture?${params.toString()}`;
  }

  return (
    <label className="flex items-center gap-2 text-xs font-semibold uppercase tracking-[0.14em] text-[var(--muted)]">
      <span>{t("language.label")}</span>
      <select
        value={locale}
        onChange={(event) => handleChange(event.target.value as Locale)}
        className="rounded-full border border-[var(--border-strong)] bg-white px-3 py-2 text-sm normal-case tracking-normal text-[var(--text)] shadow-[0_12px_28px_rgba(41,90,160,0.08)]"
      >
        {SUPPORTED_LOCALES.map((item) => (
          <option key={item} value={item}>
            {t(`language.${item}`)}
          </option>
        ))}
      </select>
    </label>
  );
}
