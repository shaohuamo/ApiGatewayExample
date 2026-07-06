import type { Metadata } from "next";
import "./globals.css";
import { QueryProvider } from "@/providers/query-provider";
import { Sidebar } from "@/components/layout/sidebar";
import { UserMenu } from "@/components/layout/user-menu";
import { LanguageSelect } from "@/components/layout/language-select";
import { auth } from "@/auth";
import { SessionExpiredRedirect } from "@/components/auth/session-expired-redirect";
import { I18nProvider } from "@/lib/i18n/provider";
import { getRequestLocale } from "@/lib/i18n/server";
import { translate } from "@/lib/i18n/dictionaries";

export async function generateMetadata(): Promise<Metadata> {
  const locale = await getRequestLocale();

  return {
    title: translate(locale, "app.title"),
    description: translate(locale, "app.description"),
  };
}

export default async function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  const session = await auth();
  const locale = await getRequestLocale();

  return (
    <html
      lang={locale}
      className="h-full antialiased"
    >
      <body className="min-h-full bg-[var(--bg)] text-[var(--text)]">
        <I18nProvider locale={locale}>
          <QueryProvider>
            <SessionExpiredRedirect hasSessionError={Boolean(session?.error)} />
            <div className="app-shell relative min-h-screen lg:flex">
              <Sidebar />
              <main className="relative z-10 flex-1 overflow-auto bg-[var(--bg-elevated)]">
                <div className="mx-auto flex min-h-screen w-full max-w-[1440px] flex-col px-4 pb-10 pt-6 sm:px-6 lg:px-10 lg:pb-14 lg:pt-8">
                  <div className="mb-4 flex flex-wrap justify-end gap-3">
                    <LanguageSelect />
                    <UserMenu userName={session?.user?.name ?? undefined} />
                  </div>
                  {children}
                </div>
              </main>
            </div>
          </QueryProvider>
        </I18nProvider>
      </body>
    </html>
  );
}
