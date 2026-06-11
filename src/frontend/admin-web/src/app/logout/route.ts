import { auth, signOut } from "@/auth";

function trimTrailingSlash(value: string) {
  return value.endsWith("/") ? value.slice(0, -1) : value;
}

function getFrontendPublicUrl() {
  return trimTrailingSlash(process.env.FRONTEND_PUBLIC_URL || "http://localhost:3000");
}

function getIdentityServerPublicUrl() {
  return trimTrailingSlash(process.env.IDENTITYSERVER_PUBLIC_URL || "http://localhost:8085");
}

export async function GET() {
  const session = await auth();
  const postLogoutRedirectUri = `${getFrontendPublicUrl()}/products`;
  const endSessionUrl = new URL(`${getIdentityServerPublicUrl()}/connect/endsession`);

  endSessionUrl.searchParams.set("post_logout_redirect_uri", postLogoutRedirectUri);

  if (session?.idToken) {
    endSessionUrl.searchParams.set("id_token_hint", session.idToken);
  }

  await signOut({ redirectTo: endSessionUrl.toString() });
}
