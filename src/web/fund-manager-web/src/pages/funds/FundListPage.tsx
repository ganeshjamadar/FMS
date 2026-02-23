import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useFunds } from '@/hooks/useFunds';
import type { FundState } from '@/types/fund';

const STATE_OPTIONS: { label: string; value: FundState | '' }[] = [
  { label: 'All', value: '' },
  { label: 'Draft', value: 'Draft' },
  { label: 'Active', value: 'Active' },
  { label: 'Dissolving', value: 'Dissolving' },
  { label: 'Dissolved', value: 'Dissolved' },
];

const STATE_BADGE: Record<string, string> = {
  Draft: 'bg-yellow-100 text-yellow-800',
  Active: 'bg-green-100 text-green-800',
  Dissolving: 'bg-orange-100 text-orange-800',
  Dissolved: 'bg-gray-100 text-gray-600',
};

export default function FundListPage() {
  const [stateFilter, setStateFilter] = useState<FundState | ''>('');
  const [page, setPage] = useState(1);
  const pageSize = 20;

  const { data, isLoading, isError, error } = useFunds({
    state: stateFilter || undefined,
    page,
    pageSize,
  });

  const totalPages = data ? Math.ceil(data.totalCount / pageSize) : 0;

  return (
    <div className="w-full px-4 sm:px-6 lg:px-8 xl:px-16 2xl:px-24 py-4 sm:py-6 lg:py-8">
      {/* Header */}
      <div className="flex flex-col sm:flex-row items-start sm:items-center justify-between gap-3 mb-4 sm:mb-6">
        <h1 className="text-xl sm:text-2xl font-bold text-gray-900">Funds</h1>
        <Link
          to="/funds/create"
          className="inline-flex items-center px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 transition-colors text-sm font-medium"
        >
          + Create Fund
        </Link>
      </div>

      {/* Filters */}
      <div className="flex flex-wrap gap-2 mb-4 sm:mb-6">
        {STATE_OPTIONS.map((opt) => (
          <button
            key={opt.value}
            onClick={() => { setStateFilter(opt.value as FundState | ''); setPage(1); }}
            className={`px-3 py-1.5 rounded-full text-sm font-medium transition-colors ${
              stateFilter === opt.value
                ? 'bg-blue-600 text-white'
                : 'bg-gray-100 text-gray-700 hover:bg-gray-200'
            }`}
          >
            {opt.label}
          </button>
        ))}
      </div>

      {/* Content */}
      {isLoading && (
        <div className="text-center py-12 text-gray-500">Loading funds...</div>
      )}

      {isError && (
        <div className="text-center py-12 text-red-600">
          Failed to load funds: {(error as Error).message}
        </div>
      )}

      {data && data.items.length === 0 && (
        <div className="text-center py-12 text-gray-500">
          No funds found.{' '}
          <Link to="/funds/create" className="text-blue-600 hover:underline">
            Create your first fund
          </Link>
        </div>
      )}

      {data && data.items.length > 0 && (
        <>
          <div className="bg-white shadow rounded-lg overflow-x-auto">
            <table className="min-w-full divide-y divide-gray-200">
              <thead className="bg-gray-50">
                <tr>
                  <th className="px-3 py-2 sm:px-6 sm:py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                    Name
                  </th>
                  <th className="px-3 py-2 sm:px-6 sm:py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                    Currency
                  </th>
                  <th className="px-3 py-2 sm:px-6 sm:py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                    Interest Rate
                  </th>
                  <th className="px-3 py-2 sm:px-6 sm:py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                    Min Contribution
                  </th>
                  <th className="px-3 py-2 sm:px-6 sm:py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                    State
                  </th>
                  <th className="px-3 py-2 sm:px-6 sm:py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                    Created
                  </th>
                </tr>
              </thead>
              <tbody className="bg-white divide-y divide-gray-200">
                {data.items.map((fund) => (
                  <tr
                    key={fund.id}
                    className="hover:bg-gray-50 cursor-pointer"
                  >
                    <td className="px-3 py-3 sm:px-6 sm:py-4 whitespace-nowrap">
                      <Link
                        to={`/funds/${fund.id}`}
                        className="text-blue-600 hover:underline font-medium"
                      >
                        {fund.name}
                      </Link>
                      {fund.description && (
                        <p className="text-sm text-gray-500 truncate max-w-xs">
                          {fund.description}
                        </p>
                      )}
                    </td>
                    <td className="px-3 py-3 sm:px-6 sm:py-4 whitespace-nowrap text-sm text-gray-700">
                      {fund.currency}
                    </td>
                    <td className="px-3 py-3 sm:px-6 sm:py-4 whitespace-nowrap text-sm text-gray-700">
                      {(fund.monthlyInterestRate * 100).toFixed(2)}%
                    </td>
                    <td className="px-3 py-3 sm:px-6 sm:py-4 whitespace-nowrap text-sm text-gray-700">
                      {fund.currency}{' '}
                      {fund.minimumMonthlyContribution.toLocaleString()}
                    </td>
                    <td className="px-3 py-3 sm:px-6 sm:py-4 whitespace-nowrap">
                      <span
                        className={`inline-flex px-2 py-0.5 text-xs font-semibold rounded-full ${
                          STATE_BADGE[fund.state] ?? 'bg-gray-100 text-gray-600'
                        }`}
                      >
                        {fund.state}
                      </span>
                    </td>
                    <td className="px-3 py-3 sm:px-6 sm:py-4 whitespace-nowrap text-sm text-gray-500">
                      {new Date(fund.createdAt).toLocaleDateString()}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          {/* Pagination */}
          {totalPages > 1 && (
            <div className="flex items-center justify-between mt-4">
              <p className="text-sm text-gray-500">
                Showing {(page - 1) * pageSize + 1}–
                {Math.min(page * pageSize, data.totalCount)} of{' '}
                {data.totalCount}
              </p>
              <div className="flex gap-2">
                <button
                  onClick={() => setPage((p) => Math.max(1, p - 1))}
                  disabled={page <= 1}
                  className="px-3 py-1 text-sm border rounded disabled:opacity-50"
                >
                  Previous
                </button>
                <button
                  onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
                  disabled={page >= totalPages}
                  className="px-3 py-1 text-sm border rounded disabled:opacity-50"
                >
                  Next
                </button>
              </div>
            </div>
          )}
        </>
      )}
    </div>
  );
}
