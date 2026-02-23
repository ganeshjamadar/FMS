import { useState, useEffect, useRef } from 'react';
import { useUpdateFund } from '@/hooks/useFunds';
import type { Fund, UpdateFundRequest } from '@/types/fund';

interface EditFundConfigModalProps {
  fund: Fund;
  open: boolean;
  onClose: () => void;
}

export default function EditFundConfigModal({
  fund,
  open,
  onClose,
}: EditFundConfigModalProps) {
  const updateFund = useUpdateFund(fund.id);
  const nameRef = useRef<HTMLInputElement>(null);

  const [form, setForm] = useState({
    name: fund.name,
    description: fund.description ?? '',
    monthlyInterestRate: (fund.monthlyInterestRate * 100).toFixed(2),
    minimumMonthlyContribution: fund.minimumMonthlyContribution.toString(),
    minimumPrincipalPerRepayment: fund.minimumPrincipalPerRepayment.toString(),
    currency: fund.currency,
    loanApprovalPolicy: fund.loanApprovalPolicy,
    maxLoanPerMember: fund.maxLoanPerMember?.toString() ?? '',
    maxConcurrentLoans: fund.maxConcurrentLoans?.toString() ?? '',
    dissolutionPolicy: fund.dissolutionPolicy ?? '',
    overduePenaltyType: fund.overduePenaltyType,
    overduePenaltyValue: fund.overduePenaltyValue.toString(),
    contributionDayOfMonth: fund.contributionDayOfMonth.toString(),
    gracePeriodDays: fund.gracePeriodDays.toString(),
  });

  useEffect(() => {
    if (open) {
      setForm({
        name: fund.name,
        description: fund.description ?? '',
        monthlyInterestRate: (fund.monthlyInterestRate * 100).toFixed(2),
        minimumMonthlyContribution: fund.minimumMonthlyContribution.toString(),
        minimumPrincipalPerRepayment: fund.minimumPrincipalPerRepayment.toString(),
        currency: fund.currency,
        loanApprovalPolicy: fund.loanApprovalPolicy,
        maxLoanPerMember: fund.maxLoanPerMember?.toString() ?? '',
        maxConcurrentLoans: fund.maxConcurrentLoans?.toString() ?? '',
        dissolutionPolicy: fund.dissolutionPolicy ?? '',
        overduePenaltyType: fund.overduePenaltyType,
        overduePenaltyValue: fund.overduePenaltyValue.toString(),
        contributionDayOfMonth: fund.contributionDayOfMonth.toString(),
        gracePeriodDays: fund.gracePeriodDays.toString(),
      });
      updateFund.reset();
      setTimeout(() => nameRef.current?.focus(), 50);
    }
  }, [open, fund]);

  if (!open) return null;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();

    const payload: UpdateFundRequest = {};
    let hasChanges = false;

    if (form.name.trim() !== fund.name) {
      payload.name = form.name.trim();
      hasChanges = true;
    }
    if ((form.description.trim() || undefined) !== (fund.description ?? undefined)) {
      payload.description = form.description.trim() || undefined;
      hasChanges = true;
    }
    const rateAsDecimal = parseFloat(form.monthlyInterestRate) / 100;
    if (rateAsDecimal !== fund.monthlyInterestRate) {
      payload.monthlyInterestRate = rateAsDecimal;
      hasChanges = true;
    }
    if (parseFloat(form.minimumMonthlyContribution) !== fund.minimumMonthlyContribution) {
      payload.minimumMonthlyContribution = parseFloat(form.minimumMonthlyContribution);
      hasChanges = true;
    }
    if (parseFloat(form.minimumPrincipalPerRepayment) !== fund.minimumPrincipalPerRepayment) {
      payload.minimumPrincipalPerRepayment = parseFloat(form.minimumPrincipalPerRepayment);
      hasChanges = true;
    }
    if (form.currency !== fund.currency) {
      payload.currency = form.currency;
      hasChanges = true;
    }
    if (form.loanApprovalPolicy !== fund.loanApprovalPolicy) {
      payload.loanApprovalPolicy = form.loanApprovalPolicy;
      hasChanges = true;
    }
    const maxLoan = form.maxLoanPerMember ? parseFloat(form.maxLoanPerMember) : null;
    if (maxLoan !== (fund.maxLoanPerMember ?? null)) {
      if (maxLoan === null) {
        payload.clearMaxLoanPerMember = true;
      } else {
        payload.maxLoanPerMember = maxLoan;
      }
      hasChanges = true;
    }
    const maxConcurrent = form.maxConcurrentLoans ? parseInt(form.maxConcurrentLoans) : null;
    if (maxConcurrent !== (fund.maxConcurrentLoans ?? null)) {
      if (maxConcurrent === null) {
        payload.clearMaxConcurrentLoans = true;
      } else {
        payload.maxConcurrentLoans = maxConcurrent;
      }
      hasChanges = true;
    }
    if (form.overduePenaltyType !== fund.overduePenaltyType) {
      payload.overduePenaltyType = form.overduePenaltyType;
      hasChanges = true;
    }
    if (parseFloat(form.overduePenaltyValue) !== fund.overduePenaltyValue) {
      payload.overduePenaltyValue = parseFloat(form.overduePenaltyValue);
      hasChanges = true;
    }
    if (parseInt(form.contributionDayOfMonth) !== fund.contributionDayOfMonth) {
      payload.contributionDayOfMonth = parseInt(form.contributionDayOfMonth);
      hasChanges = true;
    }
    if (parseInt(form.gracePeriodDays) !== fund.gracePeriodDays) {
      payload.gracePeriodDays = parseInt(form.gracePeriodDays);
      hasChanges = true;
    }

    if (!hasChanges) {
      onClose();
      return;
    }

    updateFund.mutate(payload, { onSuccess: () => onClose() });
  };

  const setField = (field: string, value: string) =>
    setForm((prev) => ({ ...prev, [field]: value }));

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center">
      <div className="absolute inset-0 bg-black/40" onClick={onClose} />
      <div className="relative bg-white rounded-xl shadow-xl w-full max-w-2xl mx-4 p-6 max-h-[90vh] overflow-y-auto">
        <h2 className="text-lg font-semibold text-gray-900 mb-4">
          Edit Fund Configuration
        </h2>
        <p className="text-xs text-amber-600 mb-4">
          Configuration can only be edited while the fund is in Draft state. Once activated, only the description can be changed.
        </p>

        <form onSubmit={handleSubmit} className="space-y-4">
          {/* Name */}
          <FieldGroup label="Fund Name *">
            <input
              ref={nameRef}
              type="text"
              value={form.name}
              onChange={(e) => setField('name', e.target.value)}
              maxLength={255}
              required
              aria-label="Fund Name"
              className="input-field"
            />
          </FieldGroup>

          {/* Description */}
          <FieldGroup label="Description">
            <textarea
              value={form.description}
              onChange={(e) => setField('description', e.target.value)}
              maxLength={500}
              rows={3}
              placeholder="Optional description"
              className="input-field resize-none"
            />
          </FieldGroup>

          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            {/* Monthly Interest Rate */}
            <FieldGroup label="Monthly Interest Rate (%) *">
              <input
                type="number"
                step="0.01"
                min="0.01"
                max="100"
                value={form.monthlyInterestRate}
                onChange={(e) => setField('monthlyInterestRate', e.target.value)}
                required
                aria-label="Monthly Interest Rate"
                className="input-field"
              />
            </FieldGroup>

            {/* Min Monthly Contribution */}
            <FieldGroup label="Min Monthly Contribution *">
              <input
                type="number"
                step="0.01"
                min="0.01"
                value={form.minimumMonthlyContribution}
                onChange={(e) => setField('minimumMonthlyContribution', e.target.value)}
                required
                aria-label="Minimum Monthly Contribution"
                className="input-field"
              />
            </FieldGroup>

            {/* Min Principal Per Repayment */}
            <FieldGroup label="Min Principal / Repayment *">
              <input
                type="number"
                step="0.01"
                min="0.01"
                value={form.minimumPrincipalPerRepayment}
                onChange={(e) => setField('minimumPrincipalPerRepayment', e.target.value)}
                required
                aria-label="Minimum Principal Per Repayment"
                className="input-field"
              />
            </FieldGroup>

            {/* Currency */}
            <FieldGroup label="Currency">
              <input
                type="text"
                value={form.currency}
                onChange={(e) => setField('currency', e.target.value)}
                aria-label="Currency"
                className="input-field"
              />
            </FieldGroup>

            {/* Loan Approval Policy */}
            <FieldGroup label="Loan Approval Policy">
              <select
                value={form.loanApprovalPolicy}
                onChange={(e) => setField('loanApprovalPolicy', e.target.value)}
                aria-label="Loan Approval Policy"
                className="input-field"
              >
                <option value="AdminOnly">Admin Only</option>
                <option value="AdminWithVoting">Admin With Voting</option>
              </select>
            </FieldGroup>

            {/* Max Loan Per Member */}
            <FieldGroup label="Max Loan / Member">
              <input
                type="number"
                step="0.01"
                min="0"
                value={form.maxLoanPerMember}
                onChange={(e) => setField('maxLoanPerMember', e.target.value)}
                placeholder="No limit"
                className="input-field"
              />
            </FieldGroup>

            {/* Max Concurrent Loans */}
            <FieldGroup label="Max Concurrent Loans">
              <input
                type="number"
                min="0"
                value={form.maxConcurrentLoans}
                onChange={(e) => setField('maxConcurrentLoans', e.target.value)}
                placeholder="No limit"
                className="input-field"
              />
            </FieldGroup>

            {/* Overdue Penalty Type */}
            <FieldGroup label="Overdue Penalty Type">
              <select
                value={form.overduePenaltyType}
                onChange={(e) => setField('overduePenaltyType', e.target.value)}
                aria-label="Overdue Penalty Type"
                className="input-field"
              >
                <option value="None">None</option>
                <option value="Flat">Flat</option>
                <option value="Percentage">Percentage</option>
              </select>
            </FieldGroup>

            {/* Overdue Penalty Value */}
            <FieldGroup label="Overdue Penalty Value">
              <input
                type="number"
                step="0.01"
                min="0"
                value={form.overduePenaltyValue}
                onChange={(e) => setField('overduePenaltyValue', e.target.value)}
                aria-label="Overdue Penalty Value"
                className="input-field"
              />
            </FieldGroup>

            {/* Contribution Day */}
            <FieldGroup label="Contribution Day of Month">
              <input
                type="number"
                min="1"
                max="28"
                value={form.contributionDayOfMonth}
                onChange={(e) => setField('contributionDayOfMonth', e.target.value)}
                required
                aria-label="Contribution Day of Month"
                className="input-field"
              />
            </FieldGroup>

            {/* Grace Period */}
            <FieldGroup label="Grace Period (days)">
              <input
                type="number"
                min="0"
                value={form.gracePeriodDays}
                onChange={(e) => setField('gracePeriodDays', e.target.value)}
                required
                aria-label="Grace Period Days"
                className="input-field"
              />
            </FieldGroup>
          </div>

          {updateFund.isError && (
            <p className="text-sm text-red-600">
              {(updateFund.error as Error).message ?? 'Failed to update fund configuration'}
            </p>
          )}

          <div className="flex justify-end gap-3 pt-2">
            <button
              type="button"
              onClick={onClose}
              disabled={updateFund.isPending}
              className="px-4 py-2 text-sm font-medium text-gray-700 bg-gray-100 rounded-lg hover:bg-gray-200 transition-colors"
            >
              Cancel
            </button>
            <button
              type="submit"
              disabled={updateFund.isPending}
              className="px-4 py-2 text-sm font-medium text-white bg-blue-600 rounded-lg hover:bg-blue-700 transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
            >
              {updateFund.isPending ? 'Saving...' : 'Save Changes'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

function FieldGroup({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <label className="block">
      <span className="block text-xs font-medium text-gray-600 mb-1">{label}</span>
      {children}
    </label>
  );
}
