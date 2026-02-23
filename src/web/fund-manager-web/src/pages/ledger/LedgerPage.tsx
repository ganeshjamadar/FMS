import { useState } from 'react';
import { useParams, Link } from 'react-router-dom';
import { useLedger } from '@/hooks/useContributions';

const typeColors: Record<string, string> = {
  Contribution: 'bg-green-100 text-green-800',
  Disbursement: 'bg-purple-100 text-purple-800',
  Repayment: 'bg-blue-100 text-blue-800',
  InterestIncome: 'bg-yellow-100 text-yellow-800',
  Penalty: 'bg-red-100 text-red-800',
  Settlement: 'bg-gray-100 text-gray-800',
};

const txTypes = ['Contribution', 'Disbursement', 'Repayment', 'InterestIncome', 'Penalty', 'Settlement'];

function formatCurrency(amount: number) {
  return new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR' }).format(amount);
}

export default function LedgerPage() {
  const { fundId } = useParams<{ fundId: string }>();
  const [typeFilter, setTypeFilter] = useState('');
  const [fromDate, setFromDate] = useState('');
  const [toDate, setToDate] = useState('');
  const [page, setPage] = useState(1);

  const { data, isLoading } = useLedger(fundId!, {
    type: typeFilter || undefined,
    fromDate: fromDate || undefined,
    toDate: toDate || undefined,
    page,
  });

  return (
    <div className="w-full px-4 sm:px-6 lg:px-8 xl:px-16 2xl:px-24 py-4 sm:py-6">
      <nav className="text-sm text-gray-500 mb-4">
        <Link to="/funds" className="hover:text-blue-600">Funds</Link>
        <span className="mx-2">/</span>
        <Link to={`/funds/${fundId}`} className="hover:text-blue-600">Fund</Link>
        <span className="mx-2">/</span>
        <span className="text-gray-900">Ledger</span>
      </nav>

      <h1 className="text-xl sm:text-2xl font-bold text-gray-900 mb-4 sm:mb-6">Fund Ledger</h1>

      {/* Filters */}
      <div className="flex flex-wrap gap-3 mb-6">
        <select
          value={typeFilter}
          onChange={(e) => { setTypeFilter(e.target.value); setPage(1); }}
          className="input"
        >
          <option value="">All Types</option>
          {txTypes.map((t) => (
            <option key={t} value={t}>{t}</option>
          ))}
        </select>
        <input
          type="date"
          value={fromDate}
          onChange={(e) => { setFromDate(e.target.value); setPage(1); }}
          className="input"
          placeholder="From"
        />
        <input
          type="date"
          value={toDate}
          onChange={(e) => { setToDate(e.target.value); setPage(1); }}
          className="input"
          placeholder="To"
        />
      </div>

      {isLoading ? (
        <p className="text-gray-500">Loading...</p>
      ) : (
        <>
          <div className="bg-white rounded-lg border overflow-x-auto">
            <table className="min-w-full divide-y divide-gray-200">
              <thead className="bg-gray-50">
                <tr>
                  <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">Date</th>
                  <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">Type</th>
                  <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">User</th>
                  <th className="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase">Amount</th>
                  <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">Description</th>
                  <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">Reference</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-200">
                {data?.items.map((tx) => (
                  <tr key={tx.id} className="hover:bg-gray-50">
                    <td className="px-4 py-3 text-sm text-gray-500">
                      {new Date(tx.createdAt).toLocaleDateString()}
                    </td>
                    <td className="px-4 py-3">
                      <span className={`px-2 py-1 text-xs font-medium rounded-full ${typeColors[tx.type] ?? 'bg-gray-100 text-gray-600'}`}>
                        {tx.type}
                      </span>
                    </td>
                    <td className="px-4 py-3 text-sm text-gray-900">{tx.userId.slice(0, 8)}...</td>
                    <td className="px-4 py-3 text-sm text-right font-medium">
                      {formatCurrency(tx.amount)}
                    </td>
                    <td className="px-4 py-3 text-sm text-gray-500">{tx.description ?? '—'}</td>
                    <td className="px-4 py-3 text-sm text-gray-400">
                      {tx.referenceEntityType ? `${tx.referenceEntityType}` : '—'}
                    </td>
                  </tr>
                ))}
                {data?.items.length === 0 && (
                  <tr>
                    <td colSpan={6} className="px-4 py-8 text-center text-gray-500">No transactions found.</td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>

          {data && data.totalCount > data.pageSize && (
            <div className="flex justify-between items-center mt-4">
              <p className="text-sm text-gray-500">
                Showing {(page - 1) * data.pageSize + 1}–{Math.min(page * data.pageSize, data.totalCount)} of {data.totalCount}
              </p>
              <div className="flex gap-2">
                <button onClick={() => setPage(page - 1)} disabled={page <= 1} className="px-3 py-1 border rounded text-sm disabled:opacity-50">Previous</button>
                <button onClick={() => setPage(page + 1)} disabled={page * data.pageSize >= data.totalCount} className="px-3 py-1 border rounded text-sm disabled:opacity-50">Next</button>
              </div>
            </div>
          )}
        </>
      )}
    </div>
  );
}
