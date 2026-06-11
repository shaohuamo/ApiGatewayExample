"use client";

import { useEffect, useRef, useState } from "react";
import { cn } from "@/lib/utils";

type UserMenuProps = {
  userName?: string;
};

export function UserMenu({ userName }: UserMenuProps) {
  const [isOpen, setIsOpen] = useState(false);
  const [isSigningOut, setIsSigningOut] = useState(false);
  const menuRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!isOpen) {
      return;
    }

    function handlePointerDown(event: PointerEvent) {
      if (!menuRef.current?.contains(event.target as Node)) {
        setIsOpen(false);
      }
    }

    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === "Escape") {
        setIsOpen(false);
      }
    }

    document.addEventListener("pointerdown", handlePointerDown);
    document.addEventListener("keydown", handleKeyDown);

    return () => {
      document.removeEventListener("pointerdown", handlePointerDown);
      document.removeEventListener("keydown", handleKeyDown);
    };
  }, [isOpen]);

  if (!userName) {
    return null;
  }

  return (
    <div className="relative" ref={menuRef}>
      <button
        type="button"
        aria-expanded={isOpen}
        aria-haspopup="menu"
        onClick={() => setIsOpen((current) => !current)}
        className="flex max-w-[min(18rem,calc(100vw-2rem))] items-center gap-3 rounded-full border border-[var(--border-strong)] bg-white px-3 py-2 text-sm font-semibold text-[var(--text)] shadow-[0_12px_28px_rgba(41,90,160,0.1)] hover:bg-[var(--surface)]"
      >
        <span className="flex h-8 w-8 shrink-0 items-center justify-center rounded-full border border-[var(--border)] bg-[var(--accent-soft)] text-[var(--accent-strong)]">
          <svg viewBox="0 0 24 24" fill="none" className="h-4 w-4" aria-hidden="true">
            <path d="M20 21a8 8 0 0 0-16 0M12 13a5 5 0 1 0 0-10 5 5 0 0 0 0 10Z" stroke="currentColor" strokeWidth="1.7" strokeLinecap="round" strokeLinejoin="round" />
          </svg>
        </span>
        <span className="min-w-0 truncate">{userName}</span>
        <svg
          viewBox="0 0 24 24"
          fill="none"
          className={cn("h-4 w-4 shrink-0 text-[var(--muted)] transition-transform", isOpen && "rotate-180")}
          aria-hidden="true"
        >
          <path d="m6 9 6 6 6-6" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" />
        </svg>
      </button>

      {isOpen && (
        <div
          role="menu"
          className="absolute right-0 top-[calc(100%+0.5rem)] z-30 w-44 rounded-[1rem] border border-[var(--border-strong)] bg-white p-1 shadow-[0_18px_36px_rgba(41,90,160,0.18)]"
        >
          <button
            type="button"
            role="menuitem"
            disabled={isSigningOut}
            onClick={() => {
              setIsSigningOut(true);
              window.location.href = "/logout";
            }}
            className="flex w-full items-center gap-3 rounded-[0.85rem] px-3 py-2.5 text-sm font-semibold text-[var(--text)] hover:bg-[var(--surface)] disabled:cursor-not-allowed disabled:opacity-60"
          >
            <svg viewBox="0 0 24 24" fill="none" className="h-4 w-4 text-[var(--muted)]" aria-hidden="true">
              <path d="M10 17l5-5-5-5M15 12H3M21 4v16" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" />
            </svg>
            <span>{isSigningOut ? "Signing out" : "Logout"}</span>
          </button>
        </div>
      )}
    </div>
  );
}
