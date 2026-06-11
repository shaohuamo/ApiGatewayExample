import { redirect } from "next/navigation";
import { auth, signIn } from "@/auth";

function getCallbackUrl(request: Request) {
  const url = new URL(request.url);
  return url.searchParams.get("callbackUrl") || "/products";
}

export async function GET(request: Request) {
  const redirectTo = getCallbackUrl(request);
  const session = await auth();

  if (session) {
    redirect(redirectTo);
  }

  await signIn("identity-server", { redirectTo });
}
