import { useState, useEffect, useRef } from 'react';
import { useUpdateFund } from '@/hooks/useFunds';

interface EditDescriptionModalProps {
  fundId: string;
  currentDescription?: string;
  open: boolean;
  onClose: () => void;
}

export default function EditDescriptionModal({
  fundId,
  currentDescription,
  open,
  onClose,
}: EditDescriptionModalProps) {
  const [description, setDescription] = useState(currentDescription ?? '');
  const updateFund = useUpdateFund(fundId);
  const textareaRef = useRef<HTMLTextAreaElement>(null);

  useEffect(() => {
    if (open) {
      setDescription(currentDescription ?? '');
      updateFund.reset();
      setTimeout(() => textareaRef.current?.focus(), 50);
    }
  }, [open, currentDescription]);

  if (!open) return null;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    const trimmed = description.trim();
    updateFund.mutate(
      { description: trimmed || undefined },
      {
        onSuccess: () => onClose(),
      },
    );
  };

  const hasChanged = (description.trim() || '') !== (currentDescription ?? '');

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center">
      {/* Backdrop */}
      <div
        className="absolute inset-0 bg-black/40"
        onClick={onClose}
      />

      {/* Modal */}
      <div className="relative bg-white rounded-xl shadow-xl w-full max-w-lg mx-4 p-6">
        <h2 className="text-lg font-semibold text-gray-900 mb-4">
          Edit Fund Description
        </h2>

        <form onSubmit={handleSubmit}>
          <textarea
            ref={textareaRef}
            value={description}
            onChange={(e) => setDescription(e.target.value)}
            maxLength={500}
            rows={4}
            placeholder="Enter fund description (optional)"
            className="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm text-gray-900 placeholder-gray-400 focus:border-blue-500 focus:ring-1 focus:ring-blue-500 resize-none"
          />
          <p className="mt-1 text-xs text-gray-400 text-right">
            {description.length}/500
          </p>

          {updateFund.isError && (
            <p className="mt-2 text-sm text-red-600">
              {(updateFund.error as Error).message ?? 'Failed to update description'}
            </p>
          )}

          <div className="mt-4 flex justify-end gap-3">
            <button
              type="button"
              onClick={onClose}
              disabled={updateFund.isPending}
              className="px-4 py-2 text-sm font-medium text-gray-700 bg-gray-100 rounded-lg hover:bg-gray-200 transition-colors"
            >
              Cancel
            </button>
            <button
              type="submit"
              disabled={updateFund.isPending || !hasChanged}
              className="px-4 py-2 text-sm font-medium text-white bg-blue-600 rounded-lg hover:bg-blue-700 transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
            >
              {updateFund.isPending ? 'Saving...' : 'Save'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
