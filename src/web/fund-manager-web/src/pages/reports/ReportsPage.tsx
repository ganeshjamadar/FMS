import { useState } from 'react';
import { useParams, Link } from 'react-router-dom';
import {
  useContributionReport,
  useLoanPortfolioReport,
  useInterestEarningsReport,
  useBalanceSheet,
  downloadReport,
} from '@/hooks/useReports';
import type { ReportType, ExportFormat } from '@/types/report';
import { usePermissions } from '@/hooks/usePermissions';

const REPORT_OPTIONS: { value: ReportType; label: string; needsDates: boolean }[] = [
  { value: 'contribution-summary', label: 'Contribution Summary', needsDates: true },
  { value: 'loan-portfolio', label: 'Loan Portfolio', needsDates: false },
  { value: 'interest-earnings', label: 'Interest Earnings', needsDates: true },
  { value: 'balance-sheet', label: 'Balance Sheet', needsDates: true },
];

function getCurrentYYYYMM(): number {
  const d = new Date();
  return d.getFullYear() * 100 + (d.getMonth() + 1);
}

function getYearStartYYYYMM(): number {
  return new Date().getFullYear() * 100 + 1;
}

export default function ReportsPage() {
  const { fundId } = useParams<{ fundId: string }>();
  const [selectedReport, setSelectedReport] = useState<ReportType>('contribution-summary');
  const [fromMonth, setFromMonth] = useState(getYearStartYYYYMM());
  const [toMonth, setToMonth] = useState(getCurrentYYYYMM());
  const [exporting, setExporting] = useState(false);
  const { canExport } = usePermissions(fundId);

  const needsDates = REPORT_OPTIONS.find(o => o.value === selectedReport)?.needsDates ?? false;

  // Queries — only enabled when selected
  const contributions = useContributionReport(
    fundId!,
    selectedReport === 'contribution-summary' ? fromMonth : 0,
    selectedReport === 'contribution-summary' ? toMonth : 0
  );
  const loanPortfolio = useLoanPortfolioReport(
    selectedReport === 'loan-portfolio' ? fundId! : ''
  );
  const interestEarnings = useInterestEarningsReport(
    fundId!,
    selectedReport === 'interest-earnings' ? fromMonth : 0,
    selectedReport === 'interest-earnings' ? toMonth : 0
  );
  const balanceSheet = useBalanceSheet(
    fundId!,
    selectedReport === 'balance-sheet' ? fromMonth : 0,
    selectedReport === 'balance-sheet' ? toMonth : 0
  );

  const handleExport = async (format: ExportFormat) => {
    if (!fundId) return;
    setExporting(true);
    try {
      const params: Record<string, string | number> = {};
      if (needsDates) {
        params.fromMonth = fromMonth;
        params.toMonth = toMonth;
      }
      await downloadReport(fundId, selectedReport, format, params);
    } finally {
      setExporting(false);
    }
  };

  const isLoading =
    (selectedReport === 'contribution-summary' && contributions.isLoading) ||
    (selectedReport === 'loan-portfolio' && loanPortfolio.isLoading) ||
    (selectedReport === 'interest-earnings' && interestEarnings.isLoading) ||
    (selectedReport === 'balance-sheet' && balanceSheet.isLoading);

  return (
    <div className="w-full px-4 sm:px-6 lg:px-8 xl:px-16 2xl:px-24 py-4 sm:py-6 lg:py-8">
      {/* Header */}
      <div className="flex flex-col sm:flex-row items-start sm:items-center justify-between gap-3 mb-4 sm:mb-6">
        <div>
          <Link to={`/funds/${fundId}`} className="text-blue-600 hover:underline text-sm">
            &larr; Back to Fund
          </Link>
          <h1 className="text-xl sm:text-2xl font-bold text-gray-900 mt-1">Reports & Exports</h1>
        </div>
      </div>

      {/* Controls */}
      <div className="bg-white rounded-lg shadow p-6 mb-6">
        <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
          {/* Report Selector */}
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Report Type</label>
            <select
              className="input w-full"
              value={selectedReport}
              onChange={e => setSelectedReport(e.target.value as ReportType)}
            >
              {REPORT_OPTIONS.map(opt => (
                <option key={opt.value} value={opt.value}>{opt.label}</option>
              ))}
            </select>
          </div>

          {/* Date Range */}
          {needsDates && (
            <>
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">From (YYYYMM)</label>
                <input
                  type="number"
                  className="input w-full"
                  value={fromMonth}
                  onChange={e => setFromMonth(Number(e.target.value))}
                  min={200001}
                  max={209912}
                />
              </div>
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">To (YYYYMM)</label>
                <input
                  type="number"
                  className="input w-full"
                  value={toMonth}
                  onChange={e => setToMonth(Number(e.target.value))}
                  min={200001}
                  max={209912}
                />
              </div>
            </>
          )}

          {/* Export Buttons */}
          {canExport && (
          <div className="flex items-end gap-2">
            <button
              onClick={() => handleExport('csv')}
              disabled={exporting}
              className="px-4 py-2 bg-green-600 text-white rounded hover:bg-green-700 disabled:opacity-50 text-sm"
            >
              {exporting ? 'Exporting...' : 'CSV'}
            </button>
            <button
              onClick={() => handleExport('pdf')}
              disabled={exporting}
              className="px-4 py-2 bg-red-600 text-white rounded hover:bg-red-700 disabled:opacity-50 text-sm"
            >
              {exporting ? 'Exporting...' : 'PDF'}
            </button>
          </div>
          )}
        </div>
      </div>

      {/* Loading */}
      {isLoading && (
        <div className="text-center py-8 text-gray-500">Loading report data...</div>
      )}

      {/* Contribution Summary */}
      {selectedReport === 'contribution-summary' && contributions.data && (
        <div className="bg-white rounded-lg shadow overflow-hidden">
          <div className="p-6 border-b">
            <h2 className="text-lg font-semibold">Contribution Summary</h2>
            <p className="text-sm text-gray-500">
              {contributions.data.fundName} &mdash; {contributions.data.fromMonth} to {contributions.data.toMonth}
            </p>
          </div>
          <div className="grid grid-cols-1 sm:grid-cols-3 gap-4 p-4 sm:p-6 bg-gray-50">
            <div className="text-center">
              <div className="text-lg sm:text-2xl font-bold text-blue-600">
                {contributions.data.totalDue.toLocaleString(undefined, { minimumFractionDigits: 2 })}
              </div>
              <div className="text-sm text-gray-500">Total Due</div>
            </div>
            <div className="text-center">
              <div className="text-lg sm:text-2xl font-bold text-green-600">
                {contributions.data.totalCollected.toLocaleString(undefined, { minimumFractionDigits: 2 })}
              </div>
              <div className="text-sm text-gray-500">Total Collected</div>
            </div>
            <div className="text-center">
              <div className="text-lg sm:text-2xl font-bold text-red-600">
                {contributions.data.totalOutstanding.toLocaleString(undefined, { minimumFractionDigits: 2 })}
              </div>
              <div className="text-sm text-gray-500">Outstanding</div>
            </div>
          </div>
          <div className="overflow-x-auto">
            <table className="min-w-full divide-y divide-gray-200">
              <thead className="bg-gray-50">
                <tr>
                  <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">Member</th>
                  <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">Month</th>
                  <th className="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase">Due</th>
                  <th className="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase">Paid</th>
                  <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">Status</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-200">
                {contributions.data.members.flatMap(member =>
                  member.months.map((month, i) => (
                    <tr key={`${member.userId}-${month.monthYear}`}>
                      {i === 0 && (
                        <td className="px-4 py-2 text-sm" rowSpan={member.months.length}>
                          {member.name}
                        </td>
                      )}
                      <td className="px-4 py-2 text-sm">{month.monthYear}</td>
                      <td className="px-4 py-2 text-sm text-right">{month.amountDue.toFixed(2)}</td>
                      <td className="px-4 py-2 text-sm text-right">{month.amountPaid.toFixed(2)}</td>
                      <td className="px-4 py-2">
                        <span className={`text-xs px-2 py-1 rounded-full ${
                          month.status === 'Paid' ? 'bg-green-100 text-green-800' :
                          month.status === 'Partial' ? 'bg-yellow-100 text-yellow-800' :
                          'bg-red-100 text-red-800'
                        }`}>
                          {month.status}
                        </span>
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* Loan Portfolio */}
      {selectedReport === 'loan-portfolio' && loanPortfolio.data && (
        <div className="bg-white rounded-lg shadow overflow-hidden">
          <div className="p-6 border-b">
            <h2 className="text-lg font-semibold">Loan Portfolio</h2>
          </div>
          <div className="grid grid-cols-1 sm:grid-cols-3 gap-4 p-4 sm:p-6 bg-gray-50">
            <div className="text-center">
              <div className="text-lg sm:text-2xl font-bold text-blue-600">{loanPortfolio.data.totalActiveLoans}</div>
              <div className="text-sm text-gray-500">Active Loans</div>
            </div>
            <div className="text-center">
              <div className="text-lg sm:text-2xl font-bold text-orange-600">
                {loanPortfolio.data.totalOutstandingPrincipal.toLocaleString(undefined, { minimumFractionDigits: 2 })}
              </div>
              <div className="text-sm text-gray-500">Outstanding Principal</div>
            </div>
            <div className="text-center">
              <div className="text-lg sm:text-2xl font-bold text-green-600">
                {loanPortfolio.data.totalInterestAccrued.toLocaleString(undefined, { minimumFractionDigits: 2 })}
              </div>
              <div className="text-sm text-gray-500">Interest Accrued</div>
            </div>
          </div>
          <div className="overflow-x-auto">
            <table className="min-w-full divide-y divide-gray-200">
              <thead className="bg-gray-50">
                <tr>
                  <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">Borrower</th>
                  <th className="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase">Principal</th>
                  <th className="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase">Outstanding</th>
                  <th className="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase">Rate</th>
                  <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">Status</th>
                  <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">Disbursed</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-200">
                {loanPortfolio.data.loans.map(loan => (
                  <tr key={loan.loanId}>
                    <td className="px-4 py-2 text-sm">{loan.borrowerName}</td>
                    <td className="px-4 py-2 text-sm text-right">{loan.principalAmount.toFixed(2)}</td>
                    <td className="px-4 py-2 text-sm text-right">{loan.outstandingPrincipal.toFixed(2)}</td>
                    <td className="px-4 py-2 text-sm text-right">{(loan.monthlyInterestRate * 100).toFixed(2)}%</td>
                    <td className="px-4 py-2">
                      <span className={`text-xs px-2 py-1 rounded-full ${
                        loan.status === 'Disbursed' || loan.status === 'Active'
                          ? 'bg-green-100 text-green-800'
                          : loan.status === 'Closed'
                          ? 'bg-gray-100 text-gray-800'
                          : 'bg-yellow-100 text-yellow-800'
                      }`}>
                        {loan.status}
                      </span>
                    </td>
                    <td className="px-4 py-2 text-sm">
                      {loan.disbursementDate
                        ? new Date(loan.disbursementDate).toLocaleDateString()
                        : 'N/A'}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* Interest Earnings */}
      {selectedReport === 'interest-earnings' && interestEarnings.data && (
        <div className="bg-white rounded-lg shadow overflow-hidden">
          <div className="p-6 border-b">
            <h2 className="text-lg font-semibold">Interest Earnings</h2>
            <p className="text-sm text-gray-500">
              Total: {interestEarnings.data.totalInterestEarned.toLocaleString(undefined, { minimumFractionDigits: 2 })}
            </p>
          </div>
          <div className="overflow-x-auto">
            <table className="min-w-full divide-y divide-gray-200">
              <thead className="bg-gray-50">
                <tr>
                  <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">Month</th>
                  <th className="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase">Interest Earned</th>
                  <th className="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase">Loan Count</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-200">
                {interestEarnings.data.months.map(month => (
                  <tr key={month.monthYear}>
                    <td className="px-4 py-2 text-sm">{month.monthYear}</td>
                    <td className="px-4 py-2 text-sm text-right">{month.interestEarned.toFixed(2)}</td>
                    <td className="px-4 py-2 text-sm text-right">{month.loanCount}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* Balance Sheet */}
      {selectedReport === 'balance-sheet' && balanceSheet.data && (
        <div className="bg-white rounded-lg shadow overflow-hidden">
          <div className="p-6 border-b">
            <h2 className="text-lg font-semibold">Balance Sheet</h2>
            <p className="text-sm text-gray-500">
              {balanceSheet.data.fundName} &mdash; {balanceSheet.data.fromMonth} to {balanceSheet.data.toMonth}
            </p>
          </div>
          <div className="p-6">
            <table className="w-full max-w-md">
              <tbody className="divide-y divide-gray-200">
                {[
                  { label: 'Opening Balance', value: balanceSheet.data.openingBalance },
                  { label: 'Contributions Received', value: balanceSheet.data.contributionsReceived, color: 'text-green-600' },
                  { label: 'Disbursements', value: balanceSheet.data.disbursements, color: 'text-red-600' },
                  { label: 'Interest Earned', value: balanceSheet.data.interestEarned, color: 'text-green-600' },
                  { label: 'Repayments Received', value: balanceSheet.data.repaymentsReceived, color: 'text-green-600' },
                  { label: 'Penalties', value: balanceSheet.data.penalties },
                ].map(item => (
                  <tr key={item.label}>
                    <td className="py-2 text-sm text-gray-700">{item.label}</td>
                    <td className={`py-2 text-sm text-right font-medium ${item.color || 'text-gray-900'}`}>
                      {item.value.toLocaleString(undefined, { minimumFractionDigits: 2 })}
                    </td>
                  </tr>
                ))}
                <tr className="border-t-2 border-gray-900">
                  <td className="py-3 text-sm font-bold text-gray-900">Closing Balance</td>
                  <td className="py-3 text-sm text-right font-bold text-blue-600">
                    {balanceSheet.data.closingBalance.toLocaleString(undefined, { minimumFractionDigits: 2 })}
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* Empty state */}
      {!isLoading &&
        ((selectedReport === 'contribution-summary' && !contributions.data) ||
         (selectedReport === 'loan-portfolio' && !loanPortfolio.data) ||
         (selectedReport === 'interest-earnings' && !interestEarnings.data) ||
         (selectedReport === 'balance-sheet' && !balanceSheet.data)) && (
        <div className="text-center py-12 text-gray-500">
          Select a report type and date range, then the data will load automatically.
        </div>
      )}
    </div>
  );
}
