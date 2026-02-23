import { useParams, Link } from 'react-router-dom';
import { useLoan, useRepayments } from '@/hooks/useLoans';
import type { LoanStatus, RepaymentStatus } from '@/types/loan';

const statusColors: Record<LoanStatus, string> = {
  PendingApproval: 'bg-yellow-100 text-yellow-800',
  Approved: 'bg-blue-100 text-blue-800',
  Active: 'bg-green-100 text-green-800',
  Closed: 'bg-gray-100 text-gray-800',
  Rejected: 'bg-red-100 text-red-800',
};

const repaymentStatusColors: Record<RepaymentStatus, string> = {
  Pending: 'bg-yellow-100 text-yellow-800',
  Paid: 'bg-green-100 text-green-800',
  Partial: 'bg-blue-100 text-blue-800',
  Overdue: 'bg-red-100 text-red-800',
};

export default function LoanDetailPage() {
  const { fundId, loanId } = useParams<{ fundId: string; loanId: string }>();
  const { data: loan, isLoading: loanLoading } = useLoan(fundId!, loanId!);
  const { data: repayments, isLoading: repaymentsLoading } = useRepayments(fundId!, loanId!);

  if (loanLoading) return <p className="p-6 text-gray-500">Loading loan details...</p>;
  if (!loan) return <p className="p-6 text-red-500">Loan not found.</p>;

  const totalInterestPaid = repayments?.reduce(
    (sum, r) => sum + Math.min(r.amountPaid, r.interestDue),
    0
  ) ?? 0;
  const totalPrincipalPaid = loan.principalAmount - loan.outstandingPrincipal;

  return (
    <div className="w-full px-4 sm:px-6 lg:px-8 xl:px-16 2xl:px-24 py-4 sm:py-6">
      <div className="flex flex-wrap items-center gap-3 sm:gap-4 mb-4 sm:mb-6">
        <Link
          to={`/funds/${fundId}/loans`}
          className="text-blue-600 hover:underline text-sm"
        >
          ← Loans
        </Link>
        <h1 className="text-xl sm:text-2xl font-bold">Loan Details</h1>
        <span className={`px-3 py-1 rounded-full text-sm ${statusColors[loan.status]}`}>
          {loan.status}
        </span>
      </div>

      {/* Loan Summary Cards */}
      <div className="grid grid-cols-2 md:grid-cols-4 gap-3 sm:gap-4 mb-6 sm:mb-8">
        <div className="bg-white border rounded-lg p-3 sm:p-4">
          <p className="text-sm text-gray-500">Principal</p>
          <p className="text-lg sm:text-xl font-bold font-mono">₹{loan.principalAmount.toLocaleString()}</p>
        </div>
        <div className="bg-white border rounded-lg p-3 sm:p-4">
          <p className="text-sm text-gray-500">Outstanding</p>
          <p className="text-lg sm:text-xl font-bold font-mono text-orange-600">
            ₹{loan.outstandingPrincipal.toLocaleString()}
          </p>
        </div>
        <div className="bg-white border rounded-lg p-3 sm:p-4">
          <p className="text-sm text-gray-500">Interest Rate</p>
          <p className="text-lg sm:text-xl font-bold">{(loan.monthlyInterestRate * 100).toFixed(2)}% / mo</p>
        </div>
        <div className="bg-white border rounded-lg p-3 sm:p-4">
          <p className="text-sm text-gray-500">Installment</p>
          <p className="text-lg sm:text-xl font-bold font-mono">₹{loan.scheduledInstallment.toLocaleString()}</p>
        </div>
      </div>

      {/* Loan Info */}
      <div className="bg-white border rounded-lg p-4 sm:p-6 mb-6 sm:mb-8">
        <h2 className="text-lg font-semibold mb-4">Loan Information</h2>
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4 text-sm">
          <div>
            <span className="text-gray-500">Borrower:</span>{' '}
            <span className="font-mono">{loan.borrowerId}</span>
          </div>
          <div>
            <span className="text-gray-500">Min. Principal:</span>{' '}
            <span className="font-mono">₹{loan.minimumPrincipal.toLocaleString()}</span>
          </div>
          <div>
            <span className="text-gray-500">Start Month:</span>{' '}
            <span>{loan.requestedStartMonth}</span>
          </div>
          {loan.purpose && (
            <div>
              <span className="text-gray-500">Purpose:</span> {loan.purpose}
            </div>
          )}
          {loan.approvalDate && (
            <div>
              <span className="text-gray-500">Approved:</span>{' '}
              {new Date(loan.approvalDate).toLocaleDateString()}
            </div>
          )}
          {loan.disbursementDate && (
            <div>
              <span className="text-gray-500">Disbursed:</span>{' '}
              {new Date(loan.disbursementDate).toLocaleDateString()}
            </div>
          )}
          {loan.closedDate && (
            <div>
              <span className="text-gray-500">Closed:</span>{' '}
              {new Date(loan.closedDate).toLocaleDateString()}
            </div>
          )}
          {loan.rejectionReason && (
            <div className="col-span-2">
              <span className="text-gray-500">Rejection Reason:</span>{' '}
              <span className="text-red-600">{loan.rejectionReason}</span>
            </div>
          )}
          <div>
            <span className="text-gray-500">Principal Repaid:</span>{' '}
            <span className="font-mono">₹{totalPrincipalPaid.toLocaleString()}</span>
          </div>
          <div>
            <span className="text-gray-500">Interest Paid:</span>{' '}
            <span className="font-mono">₹{totalInterestPaid.toLocaleString()}</span>
          </div>
        </div>
      </div>

      {/* Repayment Schedule */}
      <div className="bg-white border rounded-lg p-4 sm:p-6">
        <div className="flex justify-between items-center mb-4">
          <h2 className="text-lg font-semibold">Repayment Schedule</h2>
          <Link
            to={`/funds/${fundId}/loans/${loanId}/repayments`}
            className="text-blue-600 hover:underline text-sm"
          >
            Manage Repayments →
          </Link>
        </div>

        {repaymentsLoading ? (
          <p className="text-gray-500">Loading repayments...</p>
        ) : !repayments?.length ? (
          <p className="text-gray-500">No repayment entries yet.</p>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full border-collapse">
              <thead>
                <tr className="border-b text-left text-sm text-gray-500">
                  <th className="py-2 px-2">Month</th>
                  <th className="py-2 px-2">Interest</th>
                  <th className="py-2 px-2">Principal</th>
                  <th className="py-2 px-2">Total Due</th>
                  <th className="py-2 px-2">Paid</th>
                  <th className="py-2 px-2">Status</th>
                </tr>
              </thead>
              <tbody>
                {repayments.map((entry) => (
                  <tr key={entry.id} className="border-b hover:bg-gray-50 text-sm">
                    <td className="py-2 px-2">{entry.monthYear}</td>
                    <td className="py-2 px-2 font-mono">₹{entry.interestDue.toLocaleString()}</td>
                    <td className="py-2 px-2 font-mono">₹{entry.principalDue.toLocaleString()}</td>
                    <td className="py-2 px-2 font-mono font-semibold">₹{entry.totalDue.toLocaleString()}</td>
                    <td className="py-2 px-2 font-mono">₹{entry.amountPaid.toLocaleString()}</td>
                    <td className="py-2 px-2">
                      <span className={`px-2 py-0.5 rounded-full text-xs ${repaymentStatusColors[entry.status]}`}>
                        {entry.status}
                      </span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  );
}
