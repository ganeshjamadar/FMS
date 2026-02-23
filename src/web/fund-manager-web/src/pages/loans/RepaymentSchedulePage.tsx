import { useState } from 'react';
import { useParams, Link } from 'react-router-dom';
import { useLoan, useRepayments, useGenerateRepayment, useRecordRepayment } from '@/hooks/useLoans';
import type { RepaymentEntry, RepaymentStatus } from '@/types/loan';

const statusColors: Record<RepaymentStatus, string> = {
  Pending: 'bg-yellow-100 text-yellow-800',
  Paid: 'bg-green-100 text-green-800',
  Partial: 'bg-blue-100 text-blue-800',
  Overdue: 'bg-red-100 text-red-800',
};

export default function RepaymentSchedulePage() {
  const { fundId, loanId } = useParams<{ fundId: string; loanId: string }>();
  const { data: loan, isLoading: loanLoading } = useLoan(fundId!, loanId!);
  const { data: repayments, isLoading: repaymentsLoading } = useRepayments(fundId!, loanId!);
  const generateMutation = useGenerateRepayment(fundId!, loanId!);
  const recordMutation = useRecordRepayment(fundId!, loanId!);

  const [generateMonth, setGenerateMonth] = useState('');
  const [payModal, setPayModal] = useState<RepaymentEntry | null>(null);
  const [payAmount, setPayAmount] = useState('');

  if (loanLoading || repaymentsLoading) return <p className="p-6 text-gray-500">Loading...</p>;
  if (!loan) return <p className="p-6 text-red-500">Loan not found.</p>;

  const handleGenerate = () => {
    const monthYear = parseInt(generateMonth.replace('-', ''), 10);
    if (!isNaN(monthYear)) {
      generateMutation.mutate(monthYear, {
        onSuccess: () => setGenerateMonth(''),
      });
    }
  };

  const handleRecordPayment = () => {
    if (!payModal || !payAmount) return;
    const amount = parseFloat(payAmount);
    if (isNaN(amount) || amount <= 0) return;

    recordMutation.mutate(
      {
        repaymentId: payModal.id,
        req: { amount },
        idempotencyKey: crypto.randomUUID(),
        version: payModal.version,
      },
      {
        onSuccess: () => {
          setPayModal(null);
          setPayAmount('');
        },
      }
    );
  };

  const totalInterest = repayments?.reduce((sum, r) => sum + r.interestDue, 0) ?? 0;
  const totalPrincipal = repayments?.reduce((sum, r) => sum + r.principalDue, 0) ?? 0;
  const totalPaid = repayments?.reduce((sum, r) => sum + r.amountPaid, 0) ?? 0;

  return (
    <div className="w-full px-4 sm:px-6 lg:px-8 xl:px-16 2xl:px-24 py-4 sm:py-6">
      {/* Header */}
      <div className="flex flex-wrap items-center gap-3 sm:gap-4 mb-4 sm:mb-6">
        <Link to={`/funds/${fundId}/loans/${loanId}`} className="text-blue-600 hover:underline text-sm">
          &larr; Back to Loan
        </Link>
        <h1 className="text-xl sm:text-2xl font-bold">Repayment Schedule</h1>
      </div>

      {/* Loan Summary */}
      <div className="grid grid-cols-2 md:grid-cols-4 gap-3 sm:gap-4 mb-4 sm:mb-6">
        <div className="bg-white rounded-lg shadow p-3 sm:p-4">
          <p className="text-sm text-gray-500">Principal</p>
          <p className="text-lg sm:text-xl font-bold">{loan.principalAmount.toLocaleString()}</p>
        </div>
        <div className="bg-white rounded-lg shadow p-3 sm:p-4">
          <p className="text-sm text-gray-500">Outstanding</p>
          <p className="text-lg sm:text-xl font-bold text-orange-600">{loan.outstandingPrincipal.toLocaleString()}</p>
        </div>
        <div className="bg-white rounded-lg shadow p-3 sm:p-4">
          <p className="text-sm text-gray-500">Total Interest</p>
          <p className="text-lg sm:text-xl font-bold text-purple-600">{totalInterest.toLocaleString()}</p>
        </div>
        <div className="bg-white rounded-lg shadow p-3 sm:p-4">
          <p className="text-sm text-gray-500">Total Paid</p>
          <p className="text-lg sm:text-xl font-bold text-green-600">{totalPaid.toLocaleString()}</p>
        </div>
      </div>

      {/* Generate Repayment */}
      {loan.status === 'Active' && (
        <div className="bg-white rounded-lg shadow p-4 mb-4 sm:mb-6 flex flex-col sm:flex-row items-start sm:items-end gap-3 sm:gap-4">
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Generate for Month</label>
            <input
              type="month"
              value={generateMonth}
              onChange={(e) => setGenerateMonth(e.target.value)}
              className="input"
            />
          </div>
          <button
            onClick={handleGenerate}
            disabled={!generateMonth || generateMutation.isPending}
            className="bg-blue-600 text-white px-4 py-2 rounded hover:bg-blue-700 disabled:opacity-50"
          >
            {generateMutation.isPending ? 'Generating...' : 'Generate'}
          </button>
          {generateMutation.isError && (
            <p className="text-red-500 text-sm">Failed to generate repayment.</p>
          )}
        </div>
      )}

      {/* Repayment Schedule Table */}
      <div className="bg-white rounded-lg shadow overflow-x-auto">
        <table className="w-full text-sm">
          <thead className="bg-gray-50 border-b">
            <tr>
              <th className="px-4 py-3 text-left font-medium text-gray-600">Month</th>
              <th className="px-4 py-3 text-right font-medium text-gray-600">Interest Due</th>
              <th className="px-4 py-3 text-right font-medium text-gray-600">Principal Due</th>
              <th className="px-4 py-3 text-right font-medium text-gray-600">Total Due</th>
              <th className="px-4 py-3 text-right font-medium text-gray-600">Paid</th>
              <th className="px-4 py-3 text-center font-medium text-gray-600">Status</th>
              <th className="px-4 py-3 text-left font-medium text-gray-600">Due Date</th>
              <th className="px-4 py-3 text-center font-medium text-gray-600">Actions</th>
            </tr>
          </thead>
          <tbody className="divide-y">
            {(!repayments || repayments.length === 0) ? (
              <tr>
                <td colSpan={8} className="px-4 py-8 text-center text-gray-500">
                  No repayment entries yet. Generate one for the current month.
                </td>
              </tr>
            ) : (
              repayments.map((entry) => {
                const monthStr = `${Math.floor(entry.monthYear / 100)}-${String(entry.monthYear % 100).padStart(2, '0')}`;
                return (
                  <tr key={entry.id} className="hover:bg-gray-50">
                    <td className="px-4 py-3 font-medium">{monthStr}</td>
                    <td className="px-4 py-3 text-right">{entry.interestDue.toLocaleString()}</td>
                    <td className="px-4 py-3 text-right">{entry.principalDue.toLocaleString()}</td>
                    <td className="px-4 py-3 text-right font-semibold">{entry.totalDue.toLocaleString()}</td>
                    <td className="px-4 py-3 text-right">{entry.amountPaid.toLocaleString()}</td>
                    <td className="px-4 py-3 text-center">
                      <span className={`px-2 py-1 rounded text-xs font-medium ${statusColors[entry.status as RepaymentStatus] || 'bg-gray-100'}`}>
                        {entry.status}
                      </span>
                    </td>
                    <td className="px-4 py-3">{entry.dueDate}</td>
                    <td className="px-4 py-3 text-center">
                      {(entry.status === 'Pending' || entry.status === 'Partial' || entry.status === 'Overdue') && (
                        <button
                          onClick={() => { setPayModal(entry); setPayAmount(String(entry.totalDue - entry.amountPaid)); }}
                          className="text-green-600 hover:underline text-sm font-medium"
                        >
                          Record Payment
                        </button>
                      )}
                    </td>
                  </tr>
                );
              })
            )}
          </tbody>
          {repayments && repayments.length > 0 && (
            <tfoot className="bg-gray-50 border-t font-semibold">
              <tr>
                <td className="px-4 py-3">Total</td>
                <td className="px-4 py-3 text-right">{totalInterest.toLocaleString()}</td>
                <td className="px-4 py-3 text-right">{totalPrincipal.toLocaleString()}</td>
                <td className="px-4 py-3 text-right">{(totalInterest + totalPrincipal).toLocaleString()}</td>
                <td className="px-4 py-3 text-right">{totalPaid.toLocaleString()}</td>
                <td colSpan={3}></td>
              </tr>
            </tfoot>
          )}
        </table>
      </div>

      {/* Record Payment Modal */}
      {payModal && (
        <div className="fixed inset-0 bg-black/40 flex items-center justify-center z-50">
          <div className="bg-white rounded-lg shadow-xl p-6 w-full max-w-md">
            <h2 className="text-lg font-bold mb-4">Record Repayment</h2>
            <p className="text-sm text-gray-600 mb-2">
              Month: {Math.floor(payModal.monthYear / 100)}-{String(payModal.monthYear % 100).padStart(2, '0')}
            </p>
            <p className="text-sm text-gray-600 mb-4">
              Remaining: {(payModal.totalDue - payModal.amountPaid).toLocaleString()}
            </p>
            <div className="mb-4">
              <label className="block text-sm font-medium text-gray-700 mb-1">Payment Amount</label>
              <input
                type="number"
                step="0.01"
                min="0.01"
                value={payAmount}
                onChange={(e) => setPayAmount(e.target.value)}
                className="input w-full"
              />
            </div>
            {recordMutation.isError && (
              <p className="text-red-500 text-sm mb-2">Failed to record payment.</p>
            )}
            <div className="flex justify-end gap-3">
              <button
                onClick={() => { setPayModal(null); setPayAmount(''); }}
                className="px-4 py-2 text-gray-600 border rounded hover:bg-gray-50"
              >
                Cancel
              </button>
              <button
                onClick={handleRecordPayment}
                disabled={recordMutation.isPending}
                className="bg-green-600 text-white px-4 py-2 rounded hover:bg-green-700 disabled:opacity-50"
              >
                {recordMutation.isPending ? 'Recording...' : 'Record Payment'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
