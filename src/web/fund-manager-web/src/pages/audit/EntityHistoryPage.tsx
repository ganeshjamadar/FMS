import { useMemo, useState } from 'react';
import { useParams, useSearchParams, Link } from 'react-router-dom';
import { useEntityHistory } from '@/hooks/useAudit';
import type { AuditLogDetail } from '@/types/audit';

function formatDate(iso: string) {
  return new Date(iso).toLocaleString();
}

function defaultDateRange() {
  const to = new Date();
  const from = new Date();
  from.setFullYear(from.getFullYear() - 1);
  return {
    from: from.toISOString().slice(0, 10),
    to: to.toISOString().slice(0, 10),
  };
}

function jsonDiff(
  before: Record<string, unknown> | null,
  after: Record<string, unknown> | null
): { key: string; before: unknown; after: unknown }[] {
  if (!before && !after) return [];
  const allKeys = new Set([
    ...Object.keys(before ?? {}),
    ...Object.keys(after ?? {}),
  ]);
  const changes: { key: string; before: unknown; after: unknown }[] = [];
  for (const key of allKeys) {
    const b = before?.[key];
    const a = after?.[key];
    if (JSON.stringify(b) !== JSON.stringify(a)) {
      changes.push({ key, before: b, after: a });
    }
  }
  return changes;
}

export default function EntityHistoryPage() {
  const { fundId } = useParams<{ fundId: string }>();
  const [searchParams] = useSearchParams();
  const entityType = searchParams.get('entityType') ?? '';
  const entityId = searchParams.get('entityId') ?? '';
  const defaults = useMemo(() => defaultDateRange(), []);
  const [fromDate, setFromDate] = useState(defaults.from);
  const [toDate, setToDate] = useState(defaults.to);
  const [expandedId, setExpandedId] = useState<string | null>(null);

  const { data, isLoading, isError } = useEntityHistory(
    fundId!,
    entityType,
    entityId,
    fromDate,
    toDate
  );

  const hasParams = entityType && entityId;

  return (
    <div className="w-full px-4 sm:px-6 lg:px-8 xl:px-16 2xl:px-24 py-4 sm:py-6">
      <div className="flex flex-col sm:flex-row items-start sm:items-center justify-between gap-3 mb-4 sm:mb-6">
        <div>
          <h1 className="text-xl sm:text-2xl font-bold text-gray-900">Entity History</h1>
          {hasParams && (
            <p className="text-sm text-gray-500 mt-1">
              <span className="font-medium">{entityType}</span>{' '}
              <span className="font-mono text-xs">{entityId}</span>
            </p>
          )}
        </div>
        <Link
          to={`/funds/${fundId}/audit`}
          className="text-sm text-blue-600 hover:underline"
        >
          &larr; Back to Audit Log
        </Link>
      </div>

      {/* Date Range */}
      <div className="bg-white rounded-lg shadow p-4 mb-6">
        <div className="grid grid-cols-2 md:grid-cols-4 gap-3">
          <div>
            <label className="block text-xs font-medium text-gray-600 mb-1">
              From
            </label>
            <input
              type="date"
              value={fromDate}
              onChange={(e) => setFromDate(e.target.value)}
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
              onChange={(e) => setToDate(e.target.value)}
              className="input text-sm w-full"
            />
          </div>
        </div>
      </div>

      {!hasParams ? (
        <div className="bg-white rounded-lg shadow p-8 text-center text-gray-500">
          Please navigate here from an audit log entry to view entity history.
        </div>
      ) : isLoading ? (
        <div className="bg-white rounded-lg shadow p-8 text-center text-gray-500">
          Loading entity history...
        </div>
      ) : isError ? (
        <div className="bg-white rounded-lg shadow p-8 text-center text-red-500">
          Failed to load entity history.
        </div>
      ) : !data || data.length === 0 ? (
        <div className="bg-white rounded-lg shadow p-8 text-center text-gray-500">
          No history found for this entity.
        </div>
      ) : (
        <div className="space-y-0">
          {/* Timeline */}
          <div className="relative">
            <div className="absolute left-6 top-0 bottom-0 w-0.5 bg-gray-200" />
            {data.map((entry: AuditLogDetail, index: number) => {
              const changes = jsonDiff(entry.beforeState, entry.afterState);
              const isExpanded = expandedId === entry.id;
              return (
                <div key={entry.id} className="relative flex gap-4 pb-6">
                  {/* Timeline dot */}
                  <div className="flex-shrink-0 z-10">
                    <div
                      className={`w-3 h-3 rounded-full mt-1.5 ml-[18px] ring-4 ring-white ${
                        index === 0
                          ? 'bg-blue-500'
                          : 'bg-gray-300'
                      }`}
                    />
                  </div>

                  {/* Content */}
                  <div className="flex-1 bg-white rounded-lg shadow p-4 ml-4">
                    <div className="flex items-center justify-between">
                      <div className="flex items-center gap-2">
                        <span className="inline-flex items-center px-2 py-0.5 rounded text-xs font-medium bg-indigo-100 text-indigo-800">
                          {entry.actionType}
                        </span>
                        <span className="text-xs text-gray-500">
                          by {entry.actorId.slice(0, 8)}...
                        </span>
                      </div>
                      <span className="text-xs text-gray-400">
                        {formatDate(entry.timestamp)}
                      </span>
                    </div>

                    {/* Changes Summary */}
                    {changes.length > 0 && (
                      <div className="mt-2">
                        <button
                          onClick={() =>
                            setExpandedId(isExpanded ? null : entry.id)
                          }
                          className="text-xs text-blue-600 hover:underline"
                        >
                          {isExpanded
                            ? 'Hide changes'
                            : `${changes.length} field${changes.length > 1 ? 's' : ''} changed`}
                        </button>
                        {isExpanded && (
                          <div className="mt-2 space-y-1">
                            {changes.map((c) => (
                              <div
                                key={c.key}
                                className="flex items-start gap-2 text-xs"
                              >
                                <span className="font-medium text-gray-700 min-w-[100px]">
                                  {c.key}
                                </span>
                                <span className="text-red-600 line-through">
                                  {c.before !== undefined
                                    ? JSON.stringify(c.before)
                                    : '(none)'}
                                </span>
                                <span className="text-gray-400">&rarr;</span>
                                <span className="text-green-600">
                                  {c.after !== undefined
                                    ? JSON.stringify(c.after)
                                    : '(none)'}
                                </span>
                              </div>
                            ))}
                          </div>
                        )}
                      </div>
                    )}

                    {changes.length === 0 && (
                      <p className="mt-2 text-xs text-gray-400">
                        No field-level changes detected.
                      </p>
                    )}

                    {/* Full State Toggle */}
                    {isExpanded && (
                      <div className="mt-3 grid grid-cols-1 md:grid-cols-2 gap-3">
                        <div>
                          <h5 className="text-xs font-medium text-gray-500 mb-1">
                            Before
                          </h5>
                          <pre className="bg-gray-50 border rounded p-2 text-xs overflow-auto max-h-40">
                            {entry.beforeState
                              ? JSON.stringify(entry.beforeState, null, 2)
                              : 'N/A'}
                          </pre>
                        </div>
                        <div>
                          <h5 className="text-xs font-medium text-gray-500 mb-1">
                            After
                          </h5>
                          <pre className="bg-gray-50 border rounded p-2 text-xs overflow-auto max-h-40">
                            {entry.afterState
                              ? JSON.stringify(entry.afterState, null, 2)
                              : 'N/A'}
                          </pre>
                        </div>
                      </div>
                    )}

                    <div className="mt-2 text-xs text-gray-400">
                      {entry.serviceName}
                      {entry.correlationId && (
                        <span className="ml-2 font-mono">
                          {entry.correlationId.slice(0, 8)}...
                        </span>
                      )}
                    </div>
                  </div>
                </div>
              );
            })}
          </div>
        </div>
      )}
    </div>
  );
}
