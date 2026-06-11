import type { Metadata } from "next";
import "./globals.css";
import { QueryProvider } from "@/providers/query-provider";
import { Sidebar } from "@/components/layout/sidebar";
import { UserMenu } from "@/components/layout/user-menu";
import { OtelInitializer } from "@/components/otel-initializer";
import { auth } from "@/auth";

export const metadata: Metadata = {
  title: "Microservices Admin",
  description: "Admin dashboard for MicroservicesDemo",
};

export default async function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  const session = await auth();

  return (
    <html
      lang="en"
      className="h-full antialiased"
    >
      <body className="min-h-full bg-[var(--bg)] text-[var(--text)]">
        <OtelInitializer />
        <QueryProvider>
          <div className="app-shell relative min-h-screen lg:flex">
            <Sidebar />
            <main className="relative z-10 flex-1 overflow-auto bg-[var(--bg-elevated)]">
              <div className="mx-auto flex min-h-screen w-full max-w-[1440px] flex-col px-4 pb-10 pt-6 sm:px-6 lg:px-10 lg:pb-14 lg:pt-8">
                <div className="mb-4 flex justify-end">
                  <UserMenu userName={session?.user?.name ?? undefined} />
                </div>
                {children}
              </div>
            </main>
          </div>
        </QueryProvider>
      </body>
    </html>
  );
}
