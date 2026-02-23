import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useCreateFund } from '@/hooks/useFunds';
import type { CreateFundRequest } from '@/types/fund';

const INITIAL_VALUES: CreateFundRequest = {
  name: '',
  description: '',
  monthlyInterestRate: 0.02,
  minimumMonthlyContribution: 1000,
  minimumPrincipalPerRepayment: 1000,
  currency: 'INR',
  loanApprovalPolicy: 'AdminOnly',
  maxLoanPerMember: undefined,
  maxConcurrentLoans: undefined,
  dissolutionPolicy: 'AdminOnly',
  overduePenaltyType: 'None',
  overduePenaltyValue: 0,
  contributionDayOfMonth: 1,
  gracePeriodDays: 5,
};

export default function CreateFundPage() {
  const navigate = useNavigate();
  const createFund = useCreateFund();
  const [form, setForm] = useState<CreateFundRequest>(INITIAL_VALUES);
  const [errors, setErrors] = useState<Record<string, string>>({});

  const set = (field: keyof CreateFundRequest, value: unknown) =>
    setForm((prev) => ({ ...prev, [field]: value }));

  const validate = (): boolean => {
    const e: Record<string, string> = {};
    if (!form.name.trim()) e.name = 'Fund name is required';
    if (form.monthlyInterestRate <= 0 || form.monthlyInterestRate > 1)
      e.monthlyInterestRate = 'Interest rate must be between 0 and 100%';
    if (form.minimumMonthlyContribution <= 0)
      e.minimumMonthlyContribution = 'Minimum contribution must be > 0';
    if (form.minimumPrincipalPerRepayment <= 0)
      e.minimumPrincipalPerRepayment = 'Minimum principal must be > 0';
    if (form.contributionDayOfMonth < 1 || form.contributionDayOfMonth > 28)
      e.contributionDayOfMonth = 'Day must be between 1 and 28';
    if (form.gracePeriodDays < 0)
      e.gracePeriodDays = 'Grace period cannot be negative';
    setErrors(e);
    return Object.keys(e).length === 0;
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!validate()) return;

    try {
      const fund = await createFund.mutateAsync(form);
      navigate(`/funds/${fund.id}`);
    } catch {
      // Error handled by mutation state
    }
  };

  return (
    <div className="w-full max-w-4xl mx-auto px-4 sm:px-6 lg:px-8 py-4 sm:py-6 lg:py-8">
      <h1 className="text-xl sm:text-2xl font-bold text-gray-900 mb-4 sm:mb-6">Create Fund</h1>

      <form onSubmit={handleSubmit} className="space-y-6">
        {/* Basic Info */}
        <section className="bg-white shadow rounded-lg p-6 space-y-4">
          <h2 className="text-lg font-semibold text-gray-800">
            Basic Information
          </h2>

          <Field label="Fund Name" error={errors.name}>
            <input
              type="text"
              value={form.name}
              onChange={(e) => set('name', e.target.value)}
              className="input"
              placeholder="e.g. Office Monthly Pool"
            />
          </Field>

          <Field label="Description (optional)">
            <textarea
              value={form.description ?? ''}
              onChange={(e) => set('description', e.target.value)}
              className="input"
              rows={3}
              placeholder="Brief description of the fund's purpose"
            />
          </Field>

          <Field label="Currency">
            <select
              value={form.currency}
              onChange={(e) => set('currency', e.target.value)}
              className="input"
            >
              <option value="INR">INR — Indian Rupee</option>
            </select>
          </Field>
        </section>

        {/* Financial Config */}
        <section className="bg-white shadow rounded-lg p-6 space-y-4">
          <h2 className="text-lg font-semibold text-gray-800">
            Financial Configuration
          </h2>

          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <Field
              label="Monthly Interest Rate (%)"
              error={errors.monthlyInterestRate}
            >
              <input
                type="number"
                step="0.01"
                min="0.01"
                max="100"
                value={(form.monthlyInterestRate * 100).toFixed(2)}
                onChange={(e) =>
                  set('monthlyInterestRate', parseFloat(e.target.value) / 100)
                }
                className="input"
              />
            </Field>

            <Field
              label="Minimum Monthly Contribution"
              error={errors.minimumMonthlyContribution}
            >
              <input
                type="number"
                step="0.01"
                min="0.01"
                value={form.minimumMonthlyContribution}
                onChange={(e) =>
                  set('minimumMonthlyContribution', parseFloat(e.target.value))
                }
                className="input"
              />
            </Field>

            <Field
              label="Minimum Principal Per Repayment"
              error={errors.minimumPrincipalPerRepayment}
            >
              <input
                type="number"
                step="0.01"
                min="0.01"
                value={form.minimumPrincipalPerRepayment}
                onChange={(e) =>
                  set(
                    'minimumPrincipalPerRepayment',
                    parseFloat(e.target.value),
                  )
                }
                className="input"
              />
            </Field>

            <Field label="Max Loan Per Member (optional)">
              <input
                type="number"
                step="0.01"
                min="0"
                value={form.maxLoanPerMember ?? ''}
                onChange={(e) =>
                  set(
                    'maxLoanPerMember',
                    e.target.value ? parseFloat(e.target.value) : undefined,
                  )
                }
                className="input"
                placeholder="No limit"
              />
            </Field>

            <Field label="Max Concurrent Loans (optional)">
              <input
                type="number"
                step="1"
                min="1"
                value={form.maxConcurrentLoans ?? ''}
                onChange={(e) =>
                  set(
                    'maxConcurrentLoans',
                    e.target.value ? parseInt(e.target.value) : undefined,
                  )
                }
                className="input"
                placeholder="No limit"
              />
            </Field>
          </div>
        </section>

        {/* Policies */}
        <section className="bg-white shadow rounded-lg p-6 space-y-4">
          <h2 className="text-lg font-semibold text-gray-800">Policies</h2>

          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <Field label="Loan Approval Policy">
              <select
                value={form.loanApprovalPolicy}
                onChange={(e) => set('loanApprovalPolicy', e.target.value)}
                className="input"
              >
                <option value="AdminOnly">Admin Only</option>
                <option value="MajorityVote">Majority Vote</option>
              </select>
            </Field>

            <Field label="Dissolution Policy">
              <select
                value={form.dissolutionPolicy}
                onChange={(e) => set('dissolutionPolicy', e.target.value)}
                className="input"
              >
                <option value="AdminOnly">Admin Only</option>
                <option value="MajorityVote">Majority Vote</option>
              </select>
            </Field>

            <Field label="Overdue Penalty Type">
              <select
                value={form.overduePenaltyType}
                onChange={(e) => set('overduePenaltyType', e.target.value)}
                className="input"
              >
                <option value="None">None</option>
                <option value="Flat">Flat</option>
                <option value="Percentage">Percentage</option>
              </select>
            </Field>

            {form.overduePenaltyType !== 'None' && (
              <Field
                label={
                  form.overduePenaltyType === 'FlatFee'
                    ? 'Penalty Amount'
                    : 'Penalty Percentage'
                }
              >
                <input
                  type="number"
                  step="0.01"
                  min="0"
                  value={form.overduePenaltyValue}
                  onChange={(e) =>
                    set('overduePenaltyValue', parseFloat(e.target.value))
                  }
                  className="input"
                />
              </Field>
            )}
          </div>
        </section>

        {/* Schedule */}
        <section className="bg-white shadow rounded-lg p-6 space-y-4">
          <h2 className="text-lg font-semibold text-gray-800">Schedule</h2>

          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <Field
              label="Contribution Day of Month (1-28)"
              error={errors.contributionDayOfMonth}
            >
              <input
                type="number"
                min="1"
                max="28"
                value={form.contributionDayOfMonth}
                onChange={(e) =>
                  set('contributionDayOfMonth', parseInt(e.target.value))
                }
                className="input"
              />
            </Field>

            <Field label="Grace Period (days)" error={errors.gracePeriodDays}>
              <input
                type="number"
                min="0"
                value={form.gracePeriodDays}
                onChange={(e) =>
                  set('gracePeriodDays', parseInt(e.target.value))
                }
                className="input"
              />
            </Field>
          </div>
        </section>

        {/* Actions */}
        {createFund.isError && (
          <div className="bg-red-50 text-red-700 p-4 rounded-lg text-sm">
            {(createFund.error as Error).message ?? 'Failed to create fund'}
          </div>
        )}

        <div className="flex justify-end gap-3">
          <button
            type="button"
            onClick={() => navigate('/funds')}
            className="px-4 py-2 text-sm font-medium text-gray-700 bg-white border border-gray-300 rounded-lg hover:bg-gray-50"
          >
            Cancel
          </button>
          <button
            type="submit"
            disabled={createFund.isPending}
            className="px-6 py-2 text-sm font-medium text-white bg-blue-600 rounded-lg hover:bg-blue-700 disabled:opacity-50"
          >
            {createFund.isPending ? 'Creating...' : 'Create Fund'}
          </button>
        </div>
      </form>
    </div>
  );
}

// ── Reusable Field Component ──

function Field({
  label,
  error,
  children,
}: {
  label: string;
  error?: string;
  children: React.ReactNode;
}) {
  return (
    <div>
      <label className="block text-sm font-medium text-gray-700 mb-1">
        {label}
      </label>
      {children}
      {error && <p className="mt-1 text-sm text-red-600">{error}</p>}
    </div>
  );
}
