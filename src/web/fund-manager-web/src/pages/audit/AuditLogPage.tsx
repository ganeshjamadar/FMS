import { useState, useMemo } from 'react';
import { useParams, Link } from 'react-router-dom';
import { useAuditLogs, useAuditLogDetail } from '@/hooks/useAudit';
import type { AuditLogSummary } from '@/types/audit';

function formatDate(iso: string) {
  return new Date(iso).toLocaleString();
}

function defaultDateRange() {
  const to = new Date();
  const from = new Date();
  from.setDate(from.getDate() - 30);
  return {
    from: from.toISOString().slice(0, 10),
    to: to.toISOString().slice(0, 10),
  };
}

export default function AuditLogPage() {
  const { fundId } = useParams<{ fundId: string }>();
  const defaults = useMemo(() => defaultDateRange(), []);
  const [fromDate, setFromDate] = useState(defaults.from);
  const [toDate, setToDate] = useState(defaults.to);
  const [page, setPage] = useState(1);
  const [actorId, setActorId] = useState('');
  const [actionType, setActionType] = useState('');
  const [entityType, setEntityType] = useState('');
  const [entityId, setEntityId] = useState('');
  const [selectedLogId, setSelectedLogId] = useState<string | null>(null);

  const filters = useMemo(
    () => ({
      ...(actorId ? { actorId } : {}),
      ...(actionType ? { actionType } : {}),
      ...(entityType ? { entityType } : {}),
      ...(entityId ? { entityId } : {}),
    }),
    [actorId, actionType, entityType, entityId]
  );

  const { data, isLoading, isError } = useAuditLogs(
    fundId!,
    fromDate,
    toDate,
    page,
    filters
  );

  const { data: detail, isLoading: detailLoading } = useAuditLogDetail(
    fundId!,
    selectedLogId ?? ''
  );

  const totalPages = data ? Math.ceil(data.totalCount / data.pageSize) : 0;

  return (
    <div className="w-full px-4 sm:px-6 lg:px-8 xl:px-16 2xl:px-24 py-4 sm:py-6">
      <div className="flex flex-col sm:flex-row items-start sm:items-center justify-between gap-3 mb-4 sm:mb-6">
        <h1 className="text-xl sm:text-2xl font-bold text-gray-900">Audit Log</h1>
        <Link
          to={`/funds/${fundId}`}
          className="text-sm text-blue-600 hover:underline"
        >
          &larr; Back to Fund
        </Link>
      </div>

      {/* Filters */}
      <div className="bg-white rounded-lg shadow p-4 mb-6">
        <div className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-6 gap-3">
          <div>
            <label className="block text-xs font-medium text-gray-600 mb-1">
              From
            </label>
            <input
              type="date"
              value={fromDate}
              onChange={(e) => {
                setFromDate(e.target.value);
                setPage(1);
              }}
              className="input text-sm w-full"
            />
          </div>
          <div>
            <label className="block text-xs font-medium text-gray-600 mb-1">
              To
            </label>
            <input
              type="date"
              value={toDate}
              onChange={(e) => {
                setToDate(e.target.value);
                setPage(1);
              }}
              className="input text-sm w-full"
            />
          </div>
          <div>
            <label className="block text-xs font-medium text-gray-600 mb-1">
              Action Type
            </label>
            <input
              type="text"
              placeholder="e.g. LoanApproved"
              value={actionType}
              onChange={(e) => {
                setActionType(e.target.value);
                setPage(1);
              }}
              className="input text-sm w-full"
            />
          </div>
          <div>
            <label className="block text-xs font-medium text-gray-600 mb-1">
              Entity Type
            </label>
            <input
              type="text"
              placeholder="e.g. Loan"
              value={entityType}
              onChange={(e) => {
                setEntityType(e.target.value);
                setPage(1);
              }}
              className="input text-sm w-full"
            />
          </div>
          <div>
            <label className="block text-xs font-medium text-gray-600 mb-1">
              Entity ID
            </label>
            <input
              type="text"
              placeholder="UUID"
              value={entityId}
              onChange={(e) => {
                setEntityId(e.target.value);
                setPage(1);
              }}
              className="input text-sm w-full"
            />
          </div>
          <div>
            <label className="block text-xs font-medium text-gray-600 mb-1">
              Actor ID
            </label>
            <input
              type="text"
              placeholder="UUID"
              value={actorId}
              onChange={(e) => {
                setActorId(e.target.value);
                setPage(1);
              }}
              className="input text-sm w-full"
            />
          </div>
        </div>
      </div>

      {/* Table */}
      <div className="bg-white rounded-lg shadow overflow-x-auto">
        {isLoading ? (
          <div className="p-8 text-center text-gray-500">Loading audit logs...</div>
        ) : isError ? (
          <div className="p-8 text-center text-red-500">
            Failed to load audit logs.
          </div>
        ) : !data || data.items.length === 0 ? (
          <div className="p-8 text-center text-gray-500">
            No audit logs found for the selected filters.
          </div>
        ) : (
          <>
            <table className="min-w-full divide-y divide-gray-200">
              <thead className="bg-gray-50">
                <tr>
                  <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">
                    Timestamp
                  </th>
                  <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">
                    Action
                  </th>
                  <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">
                    Entity
                  </th>
                  <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">
                    Service
                  </th>
                  <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">
                    Actor
                  </th>
                  <th className="px-4 py-3" />
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {data.items.map((log: AuditLogSummary) => (
                  <tr
                    key={log.id}
                    className={`hover:bg-gray-50 cursor-pointer ${
                      selectedLogId === log.id ? 'bg-blue-50' : ''
                    }`}
                    onClick={() =>
                      setSelectedLogId(selectedLogId === log.id ? null : log.id)
                    }
                  >
                    <td className="px-4 py-3 text-sm text-gray-700 whitespace-nowrap">
                      {formatDate(log.timestamp)}
                    </td>
                    <td className="px-4 py-3 text-sm">
                      <span className="inline-flex items-center px-2 py-0.5 rounded text-xs font-medium bg-indigo-100 text-indigo-800">
                        {log.actionType}
                      </span>
                    </td>
                    <td className="px-4 py-3 text-sm text-gray-700">
                      <span className="font-medium">{log.entityType}</span>
                      <span className="ml-1 text-gray-400 text-xs">
                        {log.entityId.slice(0, 8)}...
                      </span>
                    </td>
                    <td className="px-4 py-3 text-sm text-gray-500">
                      {log.serviceName}
                    </td>
                    <td className="px-4 py-3 text-sm text-gray-500">
                      {log.actorId.slice(0, 8)}...
                    </td>
                    <td className="px-4 py-3 text-sm text-right">
                      <button className="text-blue-600 hover:underline text-xs">
                        {selectedLogId === log.id ? 'Hide' : 'Details'}
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>

            {/* Inline Detail Panel */}
            {selectedLogId && (
              <div className="border-t border-gray-200 bg-gray-50 p-4">
                {detailLoading ? (
                  <p className="text-sm text-gray-500">Loading detail...</p>
                ) : detail ? (
                  <div className="space-y-3">
                    <div className="flex flex-wrap gap-4 text-sm">
                      <div>
                        <span className="text-gray-500">Correlation ID:</span>{' '}
                        <span className="font-mono text-xs">
                          {detail.correlationId ?? 'N/A'}
                        </span>
                      </div>
                      <div>
                        <span className="text-gray-500">IP:</span>{' '}
                        {detail.ipAddress ?? 'N/A'}
                      </div>
                      <div>
                        <span className="text-gray-500">User Agent:</span>{' '}
                        <span className="text-xs truncate max-w-xs inline-block align-bottom">
                          {detail.userAgent ?? 'N/A'}
                        </span>
                      </div>
                    </div>
                    <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                      <div>
                        <h4 className="text-xs font-medium text-gray-500 mb-1">
                          Before State
                        </h4>
                        <pre className="bg-white border rounded p-2 text-xs overflow-auto max-h-48">
                          {detail.beforeState
                            ? JSON.stringify(detail.beforeState, null, 2)
                            : 'N/A'}
                        </pre>
                      </div>
                      <div>
                        <h4 className="text-xs font-medium text-gray-500 mb-1">
                          After State
                        </h4>
                        <pre className="bg-white border rounded p-2 text-xs overflow-auto max-h-48">
                          {detail.afterState
                            ? JSON.stringify(detail.afterState, null, 2)
                            : 'N/A'}
                        </pre>
                      </div>
                    </div>
                    <Link
                      to={`/funds/${fundId}/audit/entity-history?entityType=${detail.entityType}&entityId=${detail.entityId}`}
                      className="text-blue-600 hover:underline text-xs"
                    >
                      View full entity history &rarr;
                    </Link>
                  </div>
                ) : null}
              </div>
            )}

            {/* Pagination */}
            {totalPages > 1 && (
              <div className="flex items-center justify-between px-4 py-3 border-t border-gray-200 bg-gray-50 text-sm">
                <span className="text-gray-500">
                  Page {page} of {totalPages} ({data.totalCount} total)
                </span>
                <div className="flex gap-2">
                  <button
                    disabled={page <= 1}
                    onClick={() => setPage((p) => p - 1)}
                    className="px-3 py-1 rounded bg-white border text-gray-700 disabled:opacity-50"
                  >
                    Previous
                  </button>
                  <button
                    disabled={page >= totalPages}
                    onClick={() => setPage((p) => p + 1)}
                    className="px-3 py-1 rounded bg-white border text-gray-700 disabled:opacity-50"
                  >
                    Next
                  </button>
                </div>
              </div>
            )}
          </>
        )}
      </div>
    </div>
  );
}
