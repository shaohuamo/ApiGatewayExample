"use client";

import { useEffect } from "react";

type SessionExpiredRedirectProps = {
  hasSessionError: boolean;
};

export function SessionExpiredRedirect({ hasSessionError }: SessionExpiredRedirectProps) {
  useEffect(() => {
    if (hasSessionError) {
      window.location.replace("/logout");
    }
  }, [hasSessionError]);

  return null;
}
