import { useState } from 'react';
import { useParams, Link } from 'react-router-dom';
import { useDues, useContributionSummary, useGenerateDues, useRecordPayment } from '@/hooks/useContributions';
import { usePermissions } from '@/hooks/usePermissions';
import type { ContributionDue, ContributionDueStatus } from '@/types/contribution';

const statusColors: Record<ContributionDueStatus, string> = {
  Pending: 'bg-yellow-100 text-yellow-800',
  Paid: 'bg-green-100 text-green-800',
  Partial: 'bg-blue-100 text-blue-800',
  Late: 'bg-orange-100 text-orange-800',
  Missed: 'bg-red-100 text-red-800',
};

function currentMonthYear() {
  const now = new Date();
  return now.getFullYear() * 100 + (now.getMonth() + 1);
}

function formatCurrency(amount: number) {
  return new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR' }).format(amount);
}

export default function ContributionDuesPage() {
  const { fundId } = useParams<{ fundId: string }>();
  const [monthYear, setMonthYear] = useState(currentMonthYear());
  const [statusFilter, setStatusFilter] = useState<string>('');
  const [page, setPage] = useState(1);

  const { data: dues, isLoading } = useDues(fundId!, { monthYear, status: statusFilter || undefined, page });
  const { data: summary } = useContributionSummary(fundId!, monthYear);
  const generateMutation = useGenerateDues(fundId!);
  const paymentMutation = useRecordPayment(fundId!);
  const { canManageFund, canWrite } = usePermissions(fundId);

  const [payingDue, setPayingDue] = useState<ContributionDue | null>(null);
  const [paymentAmount, setPaymentAmount] = useState('');

  const handleGenerate = () => {
    if (confirm(`Generate contribution dues for ${monthYear}?`)) {
      generateMutation.mutate(monthYear);
    }
  };

  const handleRecordPayment = () => {
    if (!payingDue || !paymentAmount) return;
    paymentMutation.mutate(
      {
        request: { dueId: payingDue.id, amount: parseFloat(paymentAmount) },
        idempotencyKey: crypto.randomUUID(),
        version: payingDue.version,
      },
      { onSuccess: () => { setPayingDue(null); setPaymentAmount(''); } },
    );
  };

  const statuses: ContributionDueStatus[] = ['Pending', 'Paid', 'Partial', 'Late', 'Missed'];

  return (
    <div className="w-full px-4 sm:px-6 lg:px-8 xl:px-16 2xl:px-24 py-4 sm:py-6">
      {/* Breadcrumb */}
      <nav className="text-sm text-gray-500 mb-4">
        <Link to="/funds" className="hover:text-blue-600">Funds</Link>
        <span className="mx-2">/</span>
        <Link to={`/funds/${fundId}`} className="hover:text-blue-600">Fund</Link>
        <span className="mx-2">/</span>
        <span className="text-gray-900">Contributions</span>
      </nav>

      <div className="flex flex-col sm:flex-row items-start sm:items-center justify-between gap-3 mb-4 sm:mb-6">
        <h1 className="text-xl sm:text-2xl font-bold text-gray-900">Contribution Dues</h1>
        <div className="flex flex-wrap items-center gap-3">
          <input
            type="number"
            value={monthYear}
            onChange={(e) => { setMonthYear(parseInt(e.target.value)); setPage(1); }}
            className="input w-32"
            placeholder="YYYYMM"
          />
          {canManageFund && (
            <button
              onClick={handleGenerate}
              disabled={generateMutation.isPending}
              className="bg-blue-600 text-white px-4 py-2 rounded-lg hover:bg-blue-700 disabled:opacity-50"
            >
              {generateMutation.isPending ? 'Generating...' : 'Generate Dues'}
            </button>
          )}
        </div>
      </div>

      {/* Summary Cards */}
      {summary && (
        <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-5 gap-3 sm:gap-4 mb-4 sm:mb-6">
          <div className="bg-white rounded-lg border p-4">
            <p className="text-xs text-gray-500 uppercase">Total Due</p>
            <p className="text-lg font-semibold text-gray-900">{formatCurrency(summary.totalDue)}</p>
          </div>
          <div className="bg-white rounded-lg border p-4">
            <p className="text-xs text-gray-500 uppercase">Collected</p>
            <p className="text-lg font-semibold text-green-600">{formatCurrency(summary.totalCollected)}</p>
          </div>
          <div className="bg-white rounded-lg border p-4">
            <p className="text-xs text-gray-500 uppercase">Outstanding</p>
            <p className="text-lg font-semibold text-red-600">{formatCurrency(summary.totalOutstanding)}</p>
          </div>
          <div className="bg-white rounded-lg border p-4">
            <p className="text-xs text-gray-500 uppercase">Paid</p>
            <p className="text-lg font-semibold">{summary.paidCount}</p>
          </div>
          <div className="bg-white rounded-lg border p-4">
            <p className="text-xs text-gray-500 uppercase">Late/Missed</p>
            <p className="text-lg font-semibold text-orange-600">{summary.lateCount + summary.missedCount}</p>
          </div>
        </div>
      )}

      {/* Status Filters */}
      <div className="flex flex-wrap gap-2 mb-4">
        <button
          onClick={() => { setStatusFilter(''); setPage(1); }}
          className={`px-3 py-1 rounded-full text-sm font-medium ${
            !statusFilter ? 'bg-blue-100 text-blue-800' : 'bg-gray-100 text-gray-600'
          }`}
        >
          All
        </button>
        {statuses.map((s) => (
          <button
            key={s}
            onClick={() => { setStatusFilter(s); setPage(1); }}
            className={`px-3 py-1 rounded-full text-sm font-medium ${
              statusFilter === s ? statusColors[s] : 'bg-gray-100 text-gray-600'
            }`}
          >
            {s}
          </button>
        ))}
      </div>

      {/* Dues Table */}
      {isLoading ? (
        <p className="text-gray-500">Loading...</p>
      ) : (
        <>
          <div className="bg-white rounded-lg border overflow-x-auto">
            <table className="min-w-full divide-y divide-gray-200">
              <thead className="bg-gray-50">
                <tr>
                  <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">User</th>
                  <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">Amount Due</th>
                  <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">Paid</th>
                  <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">Balance</th>
                  <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">Status</th>
                  <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">Due Date</th>
                  <th className="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase">Actions</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-200">
                {dues?.items.map((due) => (
                  <tr key={due.id} className="hover:bg-gray-50">
                    <td className="px-4 py-3 text-sm text-gray-900">{due.userId.slice(0, 8)}...</td>
                    <td className="px-4 py-3 text-sm">{formatCurrency(due.amountDue)}</td>
                    <td className="px-4 py-3 text-sm text-green-600">{formatCurrency(due.amountPaid)}</td>
                    <td className="px-4 py-3 text-sm text-red-600">{formatCurrency(due.remainingBalance)}</td>
                    <td className="px-4 py-3">
                      <span className={`px-2 py-1 text-xs font-medium rounded-full ${statusColors[due.status]}`}>
                        {due.status}
                      </span>
                    </td>
                    <td className="px-4 py-3 text-sm text-gray-500">{due.dueDate}</td>
                    <td className="px-4 py-3 text-right">
                      {canWrite && due.status !== 'Paid' && due.status !== 'Missed' && (
                        <button
                          onClick={() => { setPayingDue(due); setPaymentAmount(String(due.remainingBalance)); }}
                          className="text-blue-600 hover:text-blue-800 text-sm font-medium"
                        >
                          Record Payment
                        </button>
                      )}
                    </td>
                  </tr>
                ))}
                {dues?.items.length === 0 && (
                  <tr>
                    <td colSpan={7} className="px-4 py-8 text-center text-gray-500">No dues found for this period.</td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>

          {/* Pagination */}
          {dues && dues.totalCount > dues.pageSize && (
            <div className="flex justify-between items-center mt-4">
              <p className="text-sm text-gray-500">
                Showing {(page - 1) * dues.pageSize + 1}–{Math.min(page * dues.pageSize, dues.totalCount)} of {dues.totalCount}
              </p>
              <div className="flex gap-2">
                <button
                  onClick={() => setPage(page - 1)}
                  disabled={page <= 1}
                  className="px-3 py-1 border rounded text-sm disabled:opacity-50"
                >
                  Previous
                </button>
                <button
                  onClick={() => setPage(page + 1)}
                  disabled={page * dues.pageSize >= dues.totalCount}
                  className="px-3 py-1 border rounded text-sm disabled:opacity-50"
                >
                  Next
                </button>
              </div>
            </div>
          )}
        </>
      )}

      {/* Record Payment Modal */}
      {payingDue && (
        <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50">
          <div className="bg-white rounded-xl p-6 w-full max-w-md">
            <h2 className="text-lg font-semibold mb-4">Record Payment</h2>
            <p className="text-sm text-gray-500 mb-2">
              Due: {formatCurrency(payingDue.amountDue)} | Balance: {formatCurrency(payingDue.remainingBalance)}
            </p>
            <label className="block text-sm font-medium text-gray-700 mb-1">Amount</label>
            <input
              type="number"
              step="0.01"
              value={paymentAmount}
              onChange={(e) => setPaymentAmount(e.target.value)}
              className="input w-full mb-4"
            />
            <div className="flex justify-end gap-3">
              <button
                onClick={() => setPayingDue(null)}
                className="px-4 py-2 text-gray-700 border rounded-lg"
              >
                Cancel
              </button>
              <button
                onClick={handleRecordPayment}
                disabled={paymentMutation.isPending}
                className="px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 disabled:opacity-50"
              >
                {paymentMutation.isPending ? 'Recording...' : 'Record'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
