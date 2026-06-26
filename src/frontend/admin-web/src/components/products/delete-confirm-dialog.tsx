"use client";

import type { ProductResponse } from "@/types/product";
import { useDeleteProduct } from "@/hooks/use-products";
import { useT } from "@/lib/i18n/provider";

interface DeleteConfirmDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  product: ProductResponse | null;
}

export function DeleteConfirmDialog({
  open,
  onOpenChange,
  product,
}: DeleteConfirmDialogProps) {
  const deleteMutation = useDeleteProduct();
  const t = useT();

  async function handleDelete() {
    if (!product) return;
    try {
      await deleteMutation.mutateAsync(product.productId);
      onOpenChange(false);
    } catch {
      // mutation error state is rendered below
    }
  }

  if (!open || !product) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center px-4 py-6 sm:px-6">
      <div
        className="dialog-backdrop fixed inset-0"
        onClick={() => onOpenChange(false)}
      />
      <div className="dialog-card relative w-full max-w-lg rounded-[2rem] p-6 sm:p-8">
        <div className="flex items-center gap-3">
          <div className="label-chip">{t("deleteDialog.label")}</div>
          <span className="soft-badge">{t("deleteDialog.badge")}</span>
        </div>
        <h2 className="font-display mt-4 text-3xl font-semibold text-[var(--text)]">{t("deleteDialog.title")}</h2>
        <p className="mt-4 text-sm leading-7 text-[var(--muted)] sm:text-base">
          {t("deleteDialog.messagePrefix")}{" "}
          <strong>{product.productName ?? "-"}</strong>
          {t("deleteDialog.messageSuffix")}
        </p>
        {deleteMutation.isError && (
          <p className="mt-5 rounded-2xl border border-[rgba(232,137,110,0.28)] bg-[var(--danger-soft)] px-4 py-3 text-sm text-[var(--danger)]">
            {deleteMutation.error?.message ?? t("deleteDialog.failed")}
          </p>
        )}
        <div className="mt-6 flex flex-col-reverse gap-3 sm:flex-row sm:justify-end">
          <button
            type="button"
            onClick={() => onOpenChange(false)}
            className="editorial-button-ghost"
          >
            {t("deleteDialog.cancel")}
          </button>
          <button
            type="button"
            onClick={handleDelete}
            disabled={deleteMutation.isPending}
            className="editorial-button-danger"
          >
            {deleteMutation.isPending ? t("deleteDialog.deleting") : t("deleteDialog.delete")}
          </button>
        </div>
      </div>
    </div>
  );
}
