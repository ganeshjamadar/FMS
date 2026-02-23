import { useState } from 'react';
import { useParams, Link } from 'react-router-dom';
import { useFundDashboard, useFund, useActivateFund } from '@/hooks/useFunds';
import { usePermissions } from '@/hooks/usePermissions';
import EditDescriptionModal from './EditDescriptionModal';
import EditFundConfigModal from './EditFundConfigModal';

const STATE_BADGE: Record<string, string> = {
  Draft: 'bg-yellow-100 text-yellow-800',
  Active: 'bg-green-100 text-green-800',
  Dissolving: 'bg-orange-100 text-orange-800',
  Dissolved: 'bg-gray-100 text-gray-600',
};

export default function FundDashboardPage() {
  const { fundId } = useParams<{ fundId: string }>();
  const { data: fund, isLoading: fundLoading } = useFund(fundId!);
  const { data: dashboard, isLoading: dashLoading } = useFundDashboard(fundId!);
  const activateFund = useActivateFund(fundId!);
  const { canManageFund } = usePermissions(fundId);
  const [editDescriptionOpen, setEditDescriptionOpen] = useState(false);
  const [editConfigOpen, setEditConfigOpen] = useState(false);

  if (fundLoading || dashLoading) {
    return (
      <div className="w-full px-4 sm:px-6 lg:px-8 xl:px-16 2xl:px-24 py-4 sm:py-6 lg:py-8 text-center text-gray-500">
        Loading fund details...
      </div>
    );
  }

  if (!fund || !dashboard) {
    return (
      <div className="w-full px-4 sm:px-6 lg:px-8 xl:px-16 2xl:px-24 py-4 sm:py-6 lg:py-8 text-center text-red-600">
        Fund not found.{' '}
        <Link to="/funds" className="text-blue-600 hover:underline">
          Back to funds
        </Link>
      </div>
    );
  }

  const handleActivate = async () => {
    if (!confirm('Activate this fund? Configuration will become immutable.')) return;
    activateFund.mutate();
  };

  return (
    <div className="w-full px-4 sm:px-6 lg:px-8 xl:px-16 2xl:px-24 py-4 sm:py-6 lg:py-8">
      {/* Breadcrumb */}
      <nav className="text-sm text-gray-500 mb-4">
        <Link to="/funds" className="hover:underline">
          Funds
        </Link>{' '}
        / {fund.name}
      </nav>

      {/* Edit Description Modal (non-Draft) */}
      <EditDescriptionModal
        fundId={fundId!}
        currentDescription={fund.description}
        open={editDescriptionOpen}
        onClose={() => setEditDescriptionOpen(false)}
      />

      {/* Edit Full Configuration Modal (Draft only) */}
      {fund.state === 'Draft' && (
        <EditFundConfigModal
          fund={fund}
          open={editConfigOpen}
          onClose={() => setEditConfigOpen(false)}
        />
      )}

      {/* Header */}
      <div className="flex flex-col sm:flex-row items-start justify-between gap-4 mb-6 sm:mb-8">
        <div>
          <h1 className="text-xl sm:text-2xl font-bold text-gray-900">{fund.name}</h1>
          <div className="mt-1 flex items-center gap-2">
            {fund.description ? (
              <p className="text-gray-500">{fund.description}</p>
            ) : (
              <p className="text-gray-400 italic">No description</p>
            )}
            {canManageFund && (
              <button
                onClick={() =>
                  fund.state === 'Draft'
                    ? setEditConfigOpen(true)
                    : setEditDescriptionOpen(true)
                }
                className="inline-flex items-center text-xs text-blue-600 hover:text-blue-800 transition-colors"
                title={fund.state === 'Draft' ? 'Edit description' : 'Edit description'}
              >
                <svg xmlns="http://www.w3.org/2000/svg" className="h-3.5 w-3.5" viewBox="0 0 20 20" fill="currentColor">
                  <path d="M13.586 3.586a2 2 0 112.828 2.828l-.793.793-2.828-2.828.793-.793zM11.379 5.793L3 14.172V17h2.828l8.38-8.379-2.83-2.828z" />
                </svg>
              </button>
            )}
          </div>
          <span
            className={`mt-2 inline-flex px-2.5 py-0.5 text-xs font-semibold rounded-full ${
              STATE_BADGE[fund.state] ?? 'bg-gray-100 text-gray-600'
            }`}
          >
            {fund.state}
          </span>
        </div>

        <div className="flex flex-wrap gap-2">
          {canManageFund && fund.state === 'Draft' && (
            <button
              onClick={handleActivate}
              disabled={activateFund.isPending}
              className="px-4 py-2 text-sm font-medium text-white bg-green-600 rounded-lg hover:bg-green-700 disabled:opacity-50"
            >
              {activateFund.isPending ? 'Activating...' : 'Activate Fund'}
            </button>
          )}
          <Link
            to={`/funds/${fundId}/members`}
            className="px-4 py-2 text-sm font-medium text-gray-700 bg-white border border-gray-300 rounded-lg hover:bg-gray-50"
          >
            Members
          </Link>
        </div>
      </div>

      {activateFund.isError && (
        <div className="mb-6 bg-red-50 text-red-700 p-4 rounded-lg text-sm">
          {(activateFund.error as Error).message ?? 'Activation failed'}
        </div>
      )}

      {/* Dashboard Stats */}
      <div className="grid grid-cols-2 md:grid-cols-4 gap-3 sm:gap-4 mb-6 sm:mb-8">
        <StatCard label="Total Balance" value={formatCurrency(dashboard.totalBalance, fund.currency)} />
        <StatCard label="Members" value={dashboard.memberCount.toString()} />
        <StatCard label="Active Loans" value={dashboard.activeLoansCount.toString()} />
        <StatCard label="Pending Approvals" value={dashboard.pendingApprovalsCount.toString()} />
        <StatCard
          label="Overdue Contributions"
          value={dashboard.overdueContributionsCount.toString()}
          alert={dashboard.overdueContributionsCount > 0}
        />
        <StatCard
          label="Overdue Repayments"
          value={dashboard.overdueRepaymentsCount.toString()}
          alert={dashboard.overdueRepaymentsCount > 0}
        />
        <StatCard
          label="This Month Collected"
          value={formatCurrency(dashboard.thisMonthContributionsCollected, fund.currency)}
        />
        <StatCard
          label="This Month Due"
          value={formatCurrency(dashboard.thisMonthContributionsDue, fund.currency)}
        />
      </div>

      {/* Fund Configuration */}
      <section className="bg-white shadow rounded-lg p-4 sm:p-6">
        <div className="flex items-center justify-between mb-4">
          <h2 className="text-lg font-semibold text-gray-800">
            Configuration
          </h2>
          {canManageFund && fund.state === 'Draft' && (
            <button
              onClick={() => setEditConfigOpen(true)}
              className="px-3 py-1.5 text-xs font-medium text-blue-600 bg-blue-50 rounded-lg hover:bg-blue-100 transition-colors"
            >
              Edit Configuration
            </button>
          )}
        </div>
        <div className="grid grid-cols-1 md:grid-cols-3 gap-y-3 gap-x-6 text-sm">
          <ConfigItem label="Currency" value={fund.currency} />
          <ConfigItem
            label="Monthly Interest Rate"
            value={`${(fund.monthlyInterestRate * 100).toFixed(2)}%`}
          />
          <ConfigItem
            label="Min Monthly Contribution"
            value={formatCurrency(fund.minimumMonthlyContribution, fund.currency)}
          />
          <ConfigItem
            label="Min Principal / Repayment"
            value={formatCurrency(fund.minimumPrincipalPerRepayment, fund.currency)}
          />
          <ConfigItem label="Loan Approval" value={fund.loanApprovalPolicy} />
          <ConfigItem
            label="Max Loan / Member"
            value={
              fund.maxLoanPerMember != null
                ? formatCurrency(fund.maxLoanPerMember, fund.currency)
                : 'No limit'
            }
          />
          <ConfigItem
            label="Max Concurrent Loans"
            value={fund.maxConcurrentLoans?.toString() ?? 'No limit'}
          />
          <ConfigItem label="Penalty Type" value={fund.overduePenaltyType} />
          <ConfigItem
            label="Contribution Day"
            value={`Day ${fund.contributionDayOfMonth}`}
          />
          <ConfigItem
            label="Grace Period"
            value={`${fund.gracePeriodDays} days`}
          />
        </div>
      </section>
    </div>
  );
}

// ── Helper Components ──

function StatCard({
  label,
  value,
  alert = false,
}: {
  label: string;
  value: string;
  alert?: boolean;
}) {
  return (
    <div
      className={`rounded-lg p-3 sm:p-4 ${
        alert ? 'bg-red-50 border border-red-200' : 'bg-white shadow'
      }`}
    >
      <p className="text-xs font-medium text-gray-500 uppercase">{label}</p>
      <p
        className={`mt-1 text-lg sm:text-xl font-bold ${
          alert ? 'text-red-600' : 'text-gray-900'
        }`}
      >
        {value}
      </p>
    </div>
  );
}

function ConfigItem({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <span className="text-gray-500">{label}:</span>{' '}
      <span className="font-medium text-gray-800">{value}</span>
    </div>
  );
}

function formatCurrency(amount: number, currency: string): string {
  return new Intl.NumberFormat('en-IN', {
    style: 'currency',
    currency,
    minimumFractionDigits: 2,
  }).format(amount);
}
