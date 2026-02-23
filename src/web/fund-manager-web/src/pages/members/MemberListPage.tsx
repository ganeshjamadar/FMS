import { useParams, Link } from 'react-router-dom';
import { useState } from 'react';
import { useFundMembers } from '@/hooks/useFunds';
import { useInvitations, useInviteMember } from '@/hooks/useInvitations';

const STATUS_BADGE: Record<string, string> = {
  Pending: 'bg-yellow-100 text-yellow-800',
  Accepted: 'bg-green-100 text-green-800',
  Declined: 'bg-red-100 text-red-800',
  Expired: 'bg-gray-100 text-gray-600',
};

export default function MemberListPage() {
  const { fundId } = useParams<{ fundId: string }>();
  const [showInviteModal, setShowInviteModal] = useState(false);

  const { data: membersData, isLoading: membersLoading } = useFundMembers(fundId!);
  const { data: invitationsData } = useInvitations(fundId!);

  return (
    <div className="w-full px-4 sm:px-6 lg:px-8 xl:px-16 2xl:px-24 py-4 sm:py-6 lg:py-8">
      {/* Breadcrumb */}
      <nav className="text-sm text-gray-500 mb-4">
        <Link to="/funds" className="hover:underline">Funds</Link>
        {' / '}
        <Link to={`/funds/${fundId}`} className="hover:underline">Dashboard</Link>
        {' / Members'}
      </nav>

      <div className="flex flex-col sm:flex-row items-start sm:items-center justify-between gap-3 mb-4 sm:mb-6">
        <h1 className="text-xl sm:text-2xl font-bold text-gray-900">Members</h1>
        <button
          onClick={() => setShowInviteModal(true)}
          className="px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 text-sm font-medium"
        >
          + Invite Member
        </button>
      </div>

      {/* Members Table */}
      {membersLoading ? (
        <div className="text-center py-12 text-gray-500">Loading...</div>
      ) : (
        <div className="bg-white shadow rounded-lg overflow-x-auto mb-8">
          <table className="min-w-full divide-y divide-gray-200">
            <thead className="bg-gray-50">
              <tr>
                <th className="px-3 py-2 sm:px-6 sm:py-3 text-left text-xs font-medium text-gray-500 uppercase">User</th>
                <th className="px-3 py-2 sm:px-6 sm:py-3 text-left text-xs font-medium text-gray-500 uppercase">Role</th>
                <th className="px-3 py-2 sm:px-6 sm:py-3 text-left text-xs font-medium text-gray-500 uppercase">Contribution</th>
                <th className="px-3 py-2 sm:px-6 sm:py-3 text-left text-xs font-medium text-gray-500 uppercase">Joined</th>
                <th className="px-3 py-2 sm:px-6 sm:py-3 text-left text-xs font-medium text-gray-500 uppercase">Status</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-200">
              {membersData?.items.map((member) => (
                <tr key={member.userId} className="hover:bg-gray-50">
                  <td className="px-3 py-3 sm:px-6 sm:py-4 text-sm font-medium text-gray-900">
                    {member.userName || member.userId.slice(0, 8)}
                  </td>
                  <td className="px-3 py-3 sm:px-6 sm:py-4 text-sm text-gray-700">
                    <span className={`inline-flex px-2 py-0.5 text-xs font-semibold rounded-full ${
                      member.role === 'Admin' ? 'bg-purple-100 text-purple-800' :
                      member.role === 'Editor' ? 'bg-blue-100 text-blue-800' :
                      'bg-gray-100 text-gray-600'
                    }`}>{member.role}</span>
                  </td>
                  <td className="px-3 py-3 sm:px-6 sm:py-4 text-sm text-gray-700">
                    {member.monthlyContributionAmount?.toLocaleString() ?? '—'}
                  </td>
                  <td className="px-3 py-3 sm:px-6 sm:py-4 text-sm text-gray-500">
                    {member.joinDate ? new Date(member.joinDate).toLocaleDateString() : '—'}
                  </td>
                  <td className="px-3 py-3 sm:px-6 sm:py-4 text-sm">
                    <span className={`inline-flex px-2 py-0.5 text-xs rounded-full ${
                      member.isActive ? 'bg-green-100 text-green-800' : 'bg-gray-100 text-gray-600'
                    }`}>{member.isActive ? 'Active' : 'Inactive'}</span>
                  </td>
                </tr>
              ))}
              {membersData?.items.length === 0 && (
                <tr>
                  <td colSpan={5} className="px-6 py-8 text-center text-gray-500">
                    No members yet. Invite someone to get started.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      )}

      {/* Pending Invitations */}
      {invitationsData && invitationsData.items.length > 0 && (
        <section>
          <h2 className="text-lg font-semibold text-gray-800 mb-3">Invitations</h2>
          <div className="bg-white shadow rounded-lg overflow-x-auto">
            <table className="min-w-full divide-y divide-gray-200">
              <thead className="bg-gray-50">
                <tr>
                  <th className="px-3 py-2 sm:px-6 sm:py-3 text-left text-xs font-medium text-gray-500 uppercase">Contact</th>
                  <th className="px-3 py-2 sm:px-6 sm:py-3 text-left text-xs font-medium text-gray-500 uppercase">Status</th>
                  <th className="px-3 py-2 sm:px-6 sm:py-3 text-left text-xs font-medium text-gray-500 uppercase">Expires</th>
                  <th className="px-3 py-2 sm:px-6 sm:py-3 text-left text-xs font-medium text-gray-500 uppercase">Sent</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-200">
                {invitationsData.items.map((inv) => (
                  <tr key={inv.id} className="hover:bg-gray-50">
                    <td className="px-3 py-3 sm:px-6 sm:py-4 text-sm text-gray-900">{inv.targetContact}</td>
                    <td className="px-3 py-3 sm:px-6 sm:py-4">
                      <span className={`inline-flex px-2 py-0.5 text-xs font-semibold rounded-full ${
                        STATUS_BADGE[inv.status] ?? 'bg-gray-100'
                      }`}>{inv.status}</span>
                    </td>
                    <td className="px-3 py-3 sm:px-6 sm:py-4 text-sm text-gray-500">
                      {new Date(inv.expiresAt).toLocaleDateString()}
                    </td>
                    <td className="px-3 py-3 sm:px-6 sm:py-4 text-sm text-gray-500">
                      {new Date(inv.createdAt).toLocaleDateString()}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>
      )}

      {/* Invite Modal */}
      {showInviteModal && (
        <InviteMemberModal
          fundId={fundId!}
          onClose={() => setShowInviteModal(false)}
        />
      )}
    </div>
  );
}

// ── Invite Member Modal ──

function InviteMemberModal({
  fundId,
  onClose,
}: {
  fundId: string;
  onClose: () => void;
}) {
  const [contact, setContact] = useState('');
  const inviteMember = useInviteMember(fundId);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!contact.trim()) return;
    try {
      await inviteMember.mutateAsync({ targetContact: contact.trim() });
      onClose();
    } catch {
      // Error shown in UI
    }
  };

  return (
    <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50">
      <div className="bg-white rounded-lg shadow-xl w-full max-w-md p-6">
        <h2 className="text-lg font-semibold text-gray-900 mb-4">Invite Member</h2>
        <form onSubmit={handleSubmit} className="space-y-4">
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">
              Phone or Email
            </label>
            <input
              type="text"
              value={contact}
              onChange={(e) => setContact(e.target.value)}
              className="input"
              placeholder="e.g. +91 9876543210 or user@email.com"
              autoFocus
            />
          </div>
          {inviteMember.isError && (
            <p className="text-sm text-red-600">
              {(inviteMember.error as Error).message ?? 'Failed to send invitation'}
            </p>
          )}
          <div className="flex justify-end gap-3">
            <button
              type="button"
              onClick={onClose}
              className="px-4 py-2 text-sm text-gray-700 border border-gray-300 rounded-lg hover:bg-gray-50"
            >
              Cancel
            </button>
            <button
              type="submit"
              disabled={inviteMember.isPending || !contact.trim()}
              className="px-4 py-2 text-sm text-white bg-blue-600 rounded-lg hover:bg-blue-700 disabled:opacity-50"
            >
              {inviteMember.isPending ? 'Sending...' : 'Send Invitation'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
