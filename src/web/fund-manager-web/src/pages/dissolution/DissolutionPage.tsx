import { useParams } from 'react-router-dom';
import {
  useSettlement,
  useInitiateDissolution,
  useRecalculateSettlement,
  useConfirmDissolution,
  useDownloadReport,
} from '@/hooks/useDissolution';
import { usePermissions } from '@/hooks/usePermissions';

export default function DissolutionPage() {
  const { fundId } = useParams<{ fundId: string }>();
  const settlement = useSettlement(fundId!);
  const initiate = useInitiateDissolution(fundId!);
  const recalculate = useRecalculateSettlement(fundId!);
  const confirm = useConfirmDissolution(fundId!);
  const download = useDownloadReport(fundId!);
  const { canManageFund, canExport } = usePermissions(fundId);

  const detail = settlement.data;
  const hasSettlement = !!detail;
  const isConfirmed = detail?.settlement.status === 'Confirmed';

  return (
    <div className="w-full px-4 sm:px-6 lg:px-8 xl:px-16 2xl:px-24 py-4 sm:py-6">
      <h1 className="text-xl sm:text-2xl font-bold mb-4 sm:mb-6">Fund Dissolution</h1>

      {/* Initiate section */}
      {!hasSettlement && canManageFund && (
        <div className="bg-white rounded-lg shadow p-6 mb-6">
          <h2 className="text-lg font-semibold mb-3">Initiate Dissolution</h2>
          <p className="text-gray-600 mb-4">
            Initiating dissolution will transition the fund to &quot;Dissolving&quot; state. New
            members, loans, and contributions will be blocked. Active loans continue repayments.
          </p>
          <button
            onClick={() => initiate.mutate()}
            disabled={initiate.isPending}
            className="bg-red-600 text-white px-6 py-2 rounded hover:bg-red-700 disabled:opacity-50"
          >
            {initiate.isPending ? 'Initiating…' : 'Initiate Dissolution'}
          </button>
          {initiate.isError && (
            <p className="text-red-600 text-sm mt-2">
              Failed to initiate dissolution. Fund may not be in Active state.
            </p>
          )}
        </div>
      )}

      {/* Settlement overview */}
      {detail && (
        <>
          <div className="bg-white rounded-lg shadow p-6 mb-6">
            <div className="flex items-center justify-between mb-4">
              <h2 className="text-lg font-semibold">Settlement Overview</h2>
              <span
                className={`px-3 py-1 rounded-full text-sm font-medium ${
                  isConfirmed
                    ? 'bg-green-100 text-green-800'
                    : detail.canConfirm
                      ? 'bg-blue-100 text-blue-800'
                      : 'bg-yellow-100 text-yellow-800'
                }`}
              >
                {detail.settlement.status}
              </span>
            </div>

            <div className="grid grid-cols-2 md:grid-cols-4 gap-3 sm:gap-4 text-sm mb-4">
              <div>
                <span className="text-gray-500">Total Interest Pool</span>
                <p className="text-lg sm:text-xl font-bold">{detail.settlement.totalInterestPool.toFixed(2)}</p>
              </div>
              <div>
                <span className="text-gray-500">Total Contributions</span>
                <p className="text-lg sm:text-xl font-bold">
                  {detail.settlement.totalContributionsCollected.toFixed(2)}
                </p>
              </div>
              <div>
                <span className="text-gray-500">Members</span>
                <p className="text-lg sm:text-xl font-bold">{detail.lineItems.length}</p>
              </div>
              <div>
                <span className="text-gray-500">Settlement Date</span>
                <p className="text-lg sm:text-xl font-bold">{detail.settlement.settlementDate ?? 'Pending'}</p>
              </div>
            </div>

            {/* Action buttons */}
            {!isConfirmed && (
              <div className="flex flex-wrap gap-3 mt-4">
                {canManageFund && (
                  <button
                    onClick={() => recalculate.mutate()}
                    disabled={recalculate.isPending}
                    className="bg-blue-600 text-white px-4 py-2 rounded hover:bg-blue-700 disabled:opacity-50"
                  >
                    {recalculate.isPending ? 'Recalculating…' : 'Recalculate'}
                  </button>
                )}
                {canManageFund && (
                  <button
                    onClick={() => confirm.mutate()}
                    disabled={confirm.isPending || !detail.canConfirm}
                    className="bg-green-600 text-white px-4 py-2 rounded hover:bg-green-700 disabled:opacity-50"
                    title={
                      !detail.canConfirm
                        ? 'Cannot confirm: some members have negative net payout'
                        : ''
                    }
                  >
                    {confirm.isPending ? 'Confirming…' : 'Confirm Dissolution'}
                  </button>
                )}
                {canExport && (
                  <button
                    onClick={() => download.mutate('csv')}
                    disabled={download.isPending}
                    className="bg-gray-600 text-white px-4 py-2 rounded hover:bg-gray-700 disabled:opacity-50"
                  >
                    Export CSV
                  </button>
                )}
                {canExport && (
                  <button
                    onClick={() => download.mutate('pdf')}
                    disabled={download.isPending}
                    className="bg-gray-600 text-white px-4 py-2 rounded hover:bg-gray-700 disabled:opacity-50"
                  >
                    Export PDF
                  </button>
                )}
              </div>
            )}

            {confirm.isError && (
              <p className="text-red-600 text-sm mt-2">
                Failed to confirm dissolution. Some members may have negative net payout.
              </p>
            )}
          </div>

          {/* Blockers */}
          {detail.blockers.length > 0 && (
            <div className="bg-red-50 border border-red-200 rounded-lg p-4 mb-6 overflow-x-auto">
              <h3 className="text-red-800 font-semibold mb-2">
                Dissolution Blockers ({detail.blockers.length})
              </h3>
              <p className="text-red-700 text-sm mb-3">
                The following members have negative net payouts and must resolve outstanding
                obligations before dissolution can be confirmed.
              </p>
              <table className="w-full text-sm">
                <thead>
                  <tr className="text-left text-red-700">
                    <th className="py-1">Member</th>
                    <th className="py-1 text-right">Net Payout</th>
                    <th className="py-1 text-right">Outstanding</th>
                  </tr>
                </thead>
                <tbody>
                  {detail.blockers.map((b) => (
                    <tr key={b.userId} className="border-t border-red-200">
                      <td className="py-1">{b.userName ?? b.userId.slice(0, 8)}</td>
                      <td className="py-1 text-right text-red-600 font-medium">
                        {b.netPayout.toFixed(2)}
                      </td>
                      <td className="py-1 text-right">{b.outstandingAmount.toFixed(2)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}

          {/* Settlement table */}
          <div className="bg-white rounded-lg shadow overflow-hidden">
            <h3 className="text-lg font-semibold p-4 border-b">Per-Member Settlement</h3>
            <div className="overflow-x-auto">
              <table className="w-full text-sm">
                <thead className="bg-gray-50">
                  <tr className="text-left text-gray-600">
                    <th className="px-4 py-3">Member</th>
                    <th className="px-4 py-3 text-right">Contributions</th>
                    <th className="px-4 py-3 text-right">Interest Share</th>
                    <th className="px-4 py-3 text-right">Gross Payout</th>
                    <th className="px-4 py-3 text-right">Loan Principal</th>
                    <th className="px-4 py-3 text-right">Unpaid Interest</th>
                    <th className="px-4 py-3 text-right">Unpaid Dues</th>
                    <th className="px-4 py-3 text-right font-bold">Net Payout</th>
                  </tr>
                </thead>
                <tbody>
                  {detail.lineItems.map((li) => (
                    <tr key={li.userId} className="border-t hover:bg-gray-50">
                      <td className="px-4 py-2">{li.userName ?? li.userId.slice(0, 8)}</td>
                      <td className="px-4 py-2 text-right">
                        {li.totalPaidContributions.toFixed(2)}
                      </td>
                      <td className="px-4 py-2 text-right">{li.interestShare.toFixed(2)}</td>
                      <td className="px-4 py-2 text-right">{li.grossPayout.toFixed(2)}</td>
                      <td className="px-4 py-2 text-right">
                        {li.outstandingLoanPrincipal.toFixed(2)}
                      </td>
                      <td className="px-4 py-2 text-right">{li.unpaidInterest.toFixed(2)}</td>
                      <td className="px-4 py-2 text-right">{li.unpaidDues.toFixed(2)}</td>
                      <td
                        className={`px-4 py-2 text-right font-bold ${
                          li.netPayout < 0 ? 'text-red-600' : 'text-green-700'
                        }`}
                      >
                        {li.netPayout.toFixed(2)}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        </>
      )}

      {settlement.isLoading && <p className="text-gray-500 mt-4">Loading settlement…</p>}
    </div>
  );
}
