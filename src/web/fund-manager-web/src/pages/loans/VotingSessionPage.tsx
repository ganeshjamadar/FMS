import { useParams, Link } from 'react-router-dom';
import { useState } from 'react';
import { useVotingSession, useStartVoting, useCastVote, useFinaliseVoting, useLoan } from '@/hooks/useLoans';
import type { VotingSessionDetail } from '@/types/loan';

export default function VotingSessionPage() {
  const { fundId, loanId } = useParams<{ fundId: string; loanId: string }>();
  const [sessionId, setSessionId] = useState('');
  const [windowHours, setWindowHours] = useState(48);

  const loan = useLoan(fundId!, loanId!);
  const voting = useVotingSession(fundId!, loanId!, sessionId);
  const startVoting = useStartVoting(fundId!);
  const castVote = useCastVote(fundId!, loanId!, sessionId);
  const finalise = useFinaliseVoting(fundId!, loanId!, sessionId);

  const session: VotingSessionDetail | undefined = voting.data;

  const handleStartVoting = async () => {
    try {
      const result = await startVoting.mutateAsync({
        loanId: loanId!,
        req: { votingWindowHours: windowHours },
      });
      setSessionId(result.id);
    } catch {
      // error handled by mutation state
    }
  };

  const handleCastVote = async (decision: 'Approve' | 'Reject') => {
    try {
      await castVote.mutateAsync({ decision });
    } catch {
      // error handled by mutation state
    }
  };

  const handleFinalise = async (decision: 'Approve' | 'Reject') => {
    try {
      await finalise.mutateAsync({ decision });
    } catch {
      // error handled by mutation state
    }
  };

  return (
    <div className="w-full px-4 sm:px-6 lg:px-8 xl:px-16 2xl:px-24 py-4 sm:py-6">
      <nav className="mb-4 text-sm text-gray-500">
        <Link to={`/funds/${fundId}/loans`} className="hover:underline">
          Loans
        </Link>
        {' / '}
        <Link to={`/funds/${fundId}/loans/${loanId}`} className="hover:underline">
          Loan Detail
        </Link>
        {' / '}
        <span className="text-gray-700">Voting</span>
      </nav>

      <h1 className="text-xl sm:text-2xl font-bold mb-4 sm:mb-6">Loan Voting Session</h1>

      {/* Loan summary */}
      {loan.data && (
        <div className="bg-white rounded-lg shadow p-4 mb-6">
          <div className="grid grid-cols-2 md:grid-cols-4 gap-4 text-sm">
            <div>
              <span className="text-gray-500">Status</span>
              <p className="font-semibold">{loan.data.status}</p>
            </div>
            <div>
              <span className="text-gray-500">Principal</span>
              <p className="font-semibold">{loan.data.principalAmount.toFixed(2)}</p>
            </div>
            <div>
              <span className="text-gray-500">Interest Rate</span>
              <p className="font-semibold">{(loan.data.monthlyInterestRate * 100).toFixed(4)}%</p>
            </div>
            <div>
              <span className="text-gray-500">Purpose</span>
              <p className="font-semibold">{loan.data.purpose ?? '—'}</p>
            </div>
          </div>
        </div>
      )}

      {/* Start voting (only if no active session) */}
      {!sessionId && (
        <div className="bg-white rounded-lg shadow p-4 mb-6">
          <h2 className="text-lg font-semibold mb-3">Start New Voting Session</h2>
          <div className="flex flex-col sm:flex-row items-start sm:items-end gap-3 sm:gap-4">
            <div>
              <label htmlFor="windowHours" className="block text-sm text-gray-600 mb-1">
                Voting Window (hours)
              </label>
              <input
                id="windowHours"
                type="number"
                min={1}
                max={720}
                value={windowHours}
                onChange={(e) => setWindowHours(Number(e.target.value))}
                className="input w-32"
              />
            </div>
            <button
              onClick={handleStartVoting}
              disabled={startVoting.isPending}
              className="bg-blue-600 text-white px-4 py-2 rounded hover:bg-blue-700 disabled:opacity-50"
            >
              {startVoting.isPending ? 'Starting…' : 'Start Voting'}
            </button>
          </div>
          {startVoting.isError && (
            <p className="text-red-600 text-sm mt-2">
              Failed to start voting session. Ensure the loan is in PendingApproval status.
            </p>
          )}
        </div>
      )}

      {/* Session ID input (for loading existing sessions) */}
      {!sessionId && (
        <div className="bg-white rounded-lg shadow p-4 mb-6">
          <h2 className="text-lg font-semibold mb-3">Load Existing Session</h2>
          <div className="flex flex-col sm:flex-row items-stretch sm:items-end gap-3 sm:gap-4">
            <div className="flex-1">
              <label htmlFor="sessionIdInput" className="block text-sm text-gray-600 mb-1">
                Session ID
              </label>
              <input
                id="sessionIdInput"
                type="text"
                placeholder="Enter voting session ID"
                className="input w-full"
                onKeyDown={(e) => {
                  if (e.key === 'Enter') {
                    const val = (e.target as HTMLInputElement).value.trim();
                    if (val) setSessionId(val);
                  }
                }}
              />
            </div>
            <button
              onClick={() => {
                const el = document.getElementById('sessionIdInput') as HTMLInputElement;
                const val = el?.value.trim();
                if (val) setSessionId(val);
              }}
              className="bg-gray-600 text-white px-4 py-2 rounded hover:bg-gray-700"
            >
              Load
            </button>
          </div>
        </div>
      )}

      {/* Voting session detail */}
      {sessionId && voting.isLoading && <p className="text-gray-500">Loading session…</p>}

      {sessionId && voting.isError && (
        <div className="bg-red-50 border border-red-200 rounded p-4 mb-6">
          <p className="text-red-700">Failed to load voting session.</p>
          <button
            onClick={() => setSessionId('')}
            className="text-sm text-red-600 underline mt-2"
          >
            Clear session
          </button>
        </div>
      )}

      {session && (
        <>
          {/* Session metadata */}
          <div className="bg-white rounded-lg shadow p-4 mb-6">
            <div className="flex items-center justify-between mb-4">
              <h2 className="text-lg font-semibold">Session Details</h2>
              <span
                className={`px-3 py-1 rounded-full text-sm font-medium ${
                  session.result === 'Pending'
                    ? 'bg-yellow-100 text-yellow-800'
                    : session.result === 'Approved'
                      ? 'bg-green-100 text-green-800'
                      : session.result === 'Rejected'
                        ? 'bg-red-100 text-red-800'
                        : 'bg-gray-100 text-gray-800'
                }`}
              >
                {session.result}
              </span>
            </div>

            <div className="grid grid-cols-2 md:grid-cols-4 gap-4 text-sm">
              <div>
                <span className="text-gray-500">Window Start</span>
                <p className="font-semibold">
                  {new Date(session.votingWindowStart).toLocaleString()}
                </p>
              </div>
              <div>
                <span className="text-gray-500">Window End</span>
                <p className="font-semibold">
                  {new Date(session.votingWindowEnd).toLocaleString()}
                </p>
              </div>
              <div>
                <span className="text-gray-500">Threshold</span>
                <p className="font-semibold">
                  {session.thresholdType} ≥ {session.thresholdValue}
                </p>
              </div>
              <div>
                <span className="text-gray-500">Total Eligible</span>
                <p className="font-semibold">{session.totalEligible}</p>
              </div>
            </div>

            {session.overrideUsed && (
              <div className="mt-3 bg-amber-50 border border-amber-200 rounded p-2 text-sm text-amber-800">
                ⚠ Admin override was used — final decision differs from natural vote tally.
              </div>
            )}
          </div>

          {/* Vote tally */}
          <div className="bg-white rounded-lg shadow p-4 mb-6">
            <h2 className="text-lg font-semibold mb-3">Vote Tally</h2>
            <div className="flex flex-wrap gap-4 sm:gap-8 mb-4">
              <div className="text-center">
                <div className="text-3xl font-bold text-green-600">{session.approveCount}</div>
                <div className="text-sm text-gray-500">Approve</div>
              </div>
              <div className="text-center">
                <div className="text-3xl font-bold text-red-600">{session.rejectCount}</div>
                <div className="text-sm text-gray-500">Reject</div>
              </div>
              <div className="text-center">
                <div className="text-3xl font-bold text-gray-400">
                  {session.totalEligible - session.approveCount - session.rejectCount}
                </div>
                <div className="text-sm text-gray-500">Abstained</div>
              </div>
            </div>

            {/* Progress bar */}
            {session.totalEligible > 0 && (
              <div className="w-full bg-gray-200 rounded-full h-4 overflow-hidden">
                <div className="h-full flex">
                  <div
                    className="bg-green-500 h-full"
                    style={{
                      width: `${(session.approveCount / session.totalEligible) * 100}%`,
                    }}
                  />
                  <div
                    className="bg-red-500 h-full"
                    style={{
                      width: `${(session.rejectCount / session.totalEligible) * 100}%`,
                    }}
                  />
                </div>
              </div>
            )}
          </div>

          {/* Cast vote (only if session pending and window open) */}
          {session.result === 'Pending' && new Date(session.votingWindowEnd) > new Date() && (
            <div className="bg-white rounded-lg shadow p-4 mb-6">
              <h2 className="text-lg font-semibold mb-3">Cast Your Vote</h2>
              <div className="flex gap-4">
                <button
                  onClick={() => handleCastVote('Approve')}
                  disabled={castVote.isPending}
                  className="flex-1 bg-green-600 text-white py-3 rounded hover:bg-green-700 disabled:opacity-50 font-medium"
                >
                  {castVote.isPending ? 'Voting…' : '✓ Approve'}
                </button>
                <button
                  onClick={() => handleCastVote('Reject')}
                  disabled={castVote.isPending}
                  className="flex-1 bg-red-600 text-white py-3 rounded hover:bg-red-700 disabled:opacity-50 font-medium"
                >
                  {castVote.isPending ? 'Voting…' : '✗ Reject'}
                </button>
              </div>
              {castVote.isError && (
                <p className="text-red-600 text-sm mt-2">
                  Failed to cast vote. You may have already voted.
                </p>
              )}
              {castVote.isSuccess && (
                <p className="text-green-600 text-sm mt-2">Vote recorded successfully.</p>
              )}
            </div>
          )}

          {/* Finalise (admin action, only if pending) */}
          {session.result === 'Pending' && (
            <div className="bg-white rounded-lg shadow p-4 mb-6">
              <h2 className="text-lg font-semibold mb-3">Finalise Voting (Admin)</h2>
              <p className="text-sm text-gray-500 mb-3">
                As admin, you can finalise the vote. If your decision differs from the natural
                tally, it will be recorded as an override.
              </p>
              <div className="flex flex-wrap gap-3">
                <button
                  onClick={() => handleFinalise('Approve')}
                  disabled={finalise.isPending}
                  className="bg-green-700 text-white px-6 py-2 rounded hover:bg-green-800 disabled:opacity-50"
                >
                  {finalise.isPending ? 'Finalising…' : 'Finalise as Approved'}
                </button>
                <button
                  onClick={() => handleFinalise('Reject')}
                  disabled={finalise.isPending}
                  className="bg-red-700 text-white px-6 py-2 rounded hover:bg-red-800 disabled:opacity-50"
                >
                  {finalise.isPending ? 'Finalising…' : 'Finalise as Rejected'}
                </button>
              </div>
              {finalise.isError && (
                <p className="text-red-600 text-sm mt-2">Failed to finalise voting session.</p>
              )}
            </div>
          )}

          {/* Votes list */}
          {session.votes.length > 0 && (
            <div className="bg-white rounded-lg shadow p-4 overflow-x-auto">
              <h2 className="text-lg font-semibold mb-3">Individual Votes</h2>
              <table className="w-full text-sm">
                <thead>
                  <tr className="border-b text-left text-gray-500">
                    <th className="py-2">Voter</th>
                    <th className="py-2">Decision</th>
                    <th className="py-2">Cast At</th>
                  </tr>
                </thead>
                <tbody>
                  {session.votes.map((v) => (
                    <tr key={v.voterId} className="border-b last:border-0">
                      <td className="py-2">{v.voterName ?? v.voterId.slice(0, 8)}</td>
                      <td className="py-2">
                        <span
                          className={
                            v.decision === 'Approve' ? 'text-green-600' : 'text-red-600'
                          }
                        >
                          {v.decision}
                        </span>
                      </td>
                      <td className="py-2 text-gray-500">
                        {new Date(v.castAt).toLocaleString()}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </>
      )}
    </div>
  );
}
