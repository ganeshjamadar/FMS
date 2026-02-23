import { useState } from 'react';
import { useParams, Link } from 'react-router-dom';
import { useLoans, useApproveLoan, useRejectLoan } from '@/hooks/useLoans';
import { usePermissions } from '@/hooks/usePermissions';
import type { LoanStatus } from '@/types/loan';

const statusColors: Record<LoanStatus, string> = {
  PendingApproval: 'bg-yellow-100 text-yellow-800',
  Approved: 'bg-blue-100 text-blue-800',
  Active: 'bg-green-100 text-green-800',
  Closed: 'bg-gray-100 text-gray-800',
  Rejected: 'bg-red-100 text-red-800',
};

export default function LoanApprovalPage() {
  const { fundId } = useParams<{ fundId: string }>();
  const [statusFilter, setStatusFilter] = useState<string>('');
  const [page, setPage] = useState(1);
  const [approveModal, setApproveModal] = useState<string | null>(null);
  const [rejectModal, setRejectModal] = useState<string | null>(null);
  const [installment, setInstallment] = useState('');
  const [rejectReason, setRejectReason] = useState('');

  const { data, isLoading } = useLoans(fundId!, {
    status: statusFilter || undefined,
    page,
    pageSize: 20,
  });

  const approveMutation = useApproveLoan(fundId!);
  const rejectMutation = useRejectLoan(fundId!);
  const { canManageFund } = usePermissions(fundId);

  const handleApprove = () => {
    if (!approveModal) return;
    approveMutation.mutate(
      { loanId: approveModal, req: { scheduledInstallment: parseFloat(installment) || 0 } },
      {
        onSuccess: () => {
          setApproveModal(null);
          setInstallment('');
        },
      }
    );
  };

  const handleReject = () => {
    if (!rejectModal) return;
    rejectMutation.mutate(
      { loanId: rejectModal, req: { reason: rejectReason } },
      {
        onSuccess: () => {
          setRejectModal(null);
          setRejectReason('');
        },
      }
    );
  };

  const statuses: LoanStatus[] = ['PendingApproval', 'Approved', 'Active', 'Closed', 'Rejected'];
  const totalPages = data ? Math.ceil(data.totalCount / 20) : 1;

  return (
    <div className="w-full px-4 sm:px-6 lg:px-8 xl:px-16 2xl:px-24 py-4 sm:py-6">
      <div className="flex flex-col sm:flex-row justify-between items-start sm:items-center gap-3 mb-4 sm:mb-6">
        <h1 className="text-xl sm:text-2xl font-bold">Loan Management</h1>
        <Link
          to={`/funds/${fundId}`}
          className="text-blue-600 hover:underline text-sm"
        >
          ← Back to Fund
        </Link>
      </div>

      {/* Status Filter */}
      <div className="flex gap-2 mb-6 flex-wrap">
        <button
          onClick={() => { setStatusFilter(''); setPage(1); }}
          className={`px-3 py-1 rounded-full text-sm ${!statusFilter ? 'bg-blue-600 text-white' : 'bg-gray-100 text-gray-700'}`}
        >
          All
        </button>
        {statuses.map((s) => (
          <button
            key={s}
            onClick={() => { setStatusFilter(s); setPage(1); }}
            className={`px-3 py-1 rounded-full text-sm ${statusFilter === s ? 'bg-blue-600 text-white' : 'bg-gray-100 text-gray-700'}`}
          >
            {s}
          </button>
        ))}
      </div>

      {isLoading ? (
        <p className="text-gray-500">Loading loans...</p>
      ) : !data?.items.length ? (
        <p className="text-gray-500">No loans found.</p>
      ) : (
        <>
          <div className="overflow-x-auto">
            <table className="w-full border-collapse">
              <thead>
                <tr className="border-b text-left text-sm text-gray-500">
                  <th className="py-3 px-2">Borrower</th>
                  <th className="py-3 px-2">Principal</th>
                  <th className="py-3 px-2">Outstanding</th>
                  <th className="py-3 px-2">Status</th>
                  <th className="py-3 px-2">Start Month</th>
                  <th className="py-3 px-2">Requested</th>
                  <th className="py-3 px-2">Actions</th>
                </tr>
              </thead>
              <tbody>
                {data.items.map((loan) => (
                  <tr key={loan.id} className="border-b hover:bg-gray-50">
                    <td className="py-3 px-2 text-sm">{loan.borrowerId.slice(0, 8)}...</td>
                    <td className="py-3 px-2 text-sm font-mono">₹{loan.principalAmount.toLocaleString()}</td>
                    <td className="py-3 px-2 text-sm font-mono">₹{loan.outstandingPrincipal.toLocaleString()}</td>
                    <td className="py-3 px-2">
                      <span className={`px-2 py-0.5 rounded-full text-xs ${statusColors[loan.status]}`}>
                        {loan.status}
                      </span>
                    </td>
                    <td className="py-3 px-2 text-sm">{loan.requestedStartMonth}</td>
                    <td className="py-3 px-2 text-sm">{new Date(loan.createdAt).toLocaleDateString()}</td>
                    <td className="py-3 px-2">
                      <div className="flex gap-2">
                        <Link
                          to={`/funds/${fundId}/loans/${loan.id}`}
                          className="text-blue-600 hover:underline text-sm"
                        >
                          View
                        </Link>
                        {canManageFund && loan.status === 'PendingApproval' && (
                          <>
                            <button
                              onClick={() => setApproveModal(loan.id)}
                              className="text-green-600 hover:underline text-sm"
                            >
                              Approve
                            </button>
                            <button
                              onClick={() => setRejectModal(loan.id)}
                              className="text-red-600 hover:underline text-sm"
                            >
                              Reject
                            </button>
                          </>
                        )}
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          {/* Pagination */}
          <div className="flex justify-between items-center mt-4">
            <span className="text-sm text-gray-500">
              {data.totalCount} loan{data.totalCount !== 1 ? 's' : ''} total
            </span>
            <div className="flex gap-2">
              <button
                onClick={() => setPage((p) => Math.max(1, p - 1))}
                disabled={page <= 1}
                className="px-3 py-1 border rounded text-sm disabled:opacity-50"
              >
                Previous
              </button>
              <span className="px-3 py-1 text-sm">
                {page} / {totalPages}
              </span>
              <button
                onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
                disabled={page >= totalPages}
                className="px-3 py-1 border rounded text-sm disabled:opacity-50"
              >
                Next
              </button>
            </div>
          </div>
        </>
      )}

      {/* Approve Modal */}
      {approveModal && (
        <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50">
          <div className="bg-white rounded-lg p-6 w-full max-w-md">
            <h2 className="text-lg font-bold mb-4">Approve Loan</h2>
            <label className="block mb-2 text-sm font-medium">
              Scheduled Monthly Installment (₹)
            </label>
            <input
              type="number"
              value={installment}
              onChange={(e) => setInstallment(e.target.value)}
              className="input w-full mb-4"
              placeholder="0 for minimum principal only"
              min="0"
              step="0.01"
            />
            <div className="flex justify-end gap-2">
              <button
                onClick={() => { setApproveModal(null); setInstallment(''); }}
                className="px-4 py-2 border rounded"
              >
                Cancel
              </button>
              <button
                onClick={handleApprove}
                disabled={approveMutation.isPending}
                className="px-4 py-2 bg-green-600 text-white rounded hover:bg-green-700 disabled:opacity-50"
              >
                {approveMutation.isPending ? 'Approving...' : 'Approve'}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Reject Modal */}
      {rejectModal && (
        <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50">
          <div className="bg-white rounded-lg p-6 w-full max-w-md">
            <h2 className="text-lg font-bold mb-4">Reject Loan</h2>
            <label className="block mb-2 text-sm font-medium">Reason</label>
            <textarea
              value={rejectReason}
              onChange={(e) => setRejectReason(e.target.value)}
              className="input w-full mb-4 h-24"
              placeholder="Explain the rejection reason..."
            />
            <div className="flex justify-end gap-2">
              <button
                onClick={() => { setRejectModal(null); setRejectReason(''); }}
                className="px-4 py-2 border rounded"
              >
                Cancel
              </button>
              <button
                onClick={handleReject}
                disabled={rejectMutation.isPending || !rejectReason.trim()}
                className="px-4 py-2 bg-red-600 text-white rounded hover:bg-red-700 disabled:opacity-50"
              >
                {rejectMutation.isPending ? 'Rejecting...' : 'Reject'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
