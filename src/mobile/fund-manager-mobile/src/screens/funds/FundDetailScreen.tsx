import React, { useState, useEffect } from 'react';
import {
  View,
  Text,
  ScrollView,
  TouchableOpacity,
  TextInput,
  StyleSheet,
  ActivityIndicator,
  Alert,
  Modal,
  RefreshControl,
} from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import apiClient from '../../services/apiClient';
import { usePermissions } from '../../hooks/usePermissions';

interface Fund {
  id: string;
  name: string;
  description?: string;
  currency: string;
  monthlyInterestRate: number;
  minimumMonthlyContribution: number;
  minimumPrincipalPerRepayment: number;
  loanApprovalPolicy: string;
  maxLoanPerMember?: number;
  maxConcurrentLoans?: number;
  overduePenaltyType: string;
  overduePenaltyValue: number;
  contributionDayOfMonth: number;
  gracePeriodDays: number;
  state: string;
  createdAt: string;
  updatedAt: string;
}

interface FundDashboard {
  totalBalance: number;
  memberCount: number;
  activeLoansCount: number;
  pendingApprovalsCount: number;
  overdueContributionsCount: number;
  overdueRepaymentsCount: number;
  thisMonthContributionsCollected: number;
  thisMonthContributionsDue: number;
}

const STATE_COLORS: Record<string, { bg: string; text: string }> = {
  Draft: { bg: '#FEF3C7', text: '#92400E' },
  Active: { bg: '#D1FAE5', text: '#065F46' },
  Dissolving: { bg: '#FFEDD5', text: '#9A3412' },
  Dissolved: { bg: '#F3F4F6', text: '#4B5563' },
};

function formatCurrency(amount: number, currency: string): string {
  return `${currency} ${amount.toLocaleString('en-IN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
}

export default function FundDetailScreen({
  route,
  navigation,
}: {
  route: { params: { fundId: string } };
  navigation: any;
}) {
  const { fundId } = route.params;
  const queryClient = useQueryClient();
  const { canManageFund } = usePermissions(fundId);
  const [editModalVisible, setEditModalVisible] = useState(false);
  const [editDescription, setEditDescription] = useState('');
  const [editConfigModalVisible, setEditConfigModalVisible] = useState(false);

  const {
    data: fund,
    isLoading: fundLoading,
    refetch: refetchFund,
    isRefetching: fundRefetching,
  } = useQuery({
    queryKey: ['funds', 'detail', fundId],
    queryFn: async () => {
      const response = await apiClient.get<Fund>(`/api/funds/${fundId}`);
      return response.data;
    },
  });

  const {
    data: dashboard,
    isLoading: dashLoading,
    refetch: refetchDashboard,
  } = useQuery({
    queryKey: ['funds', 'dashboard', fundId],
    queryFn: async () => {
      const response = await apiClient.get<FundDashboard>(`/api/funds/${fundId}/dashboard`);
      return response.data;
    },
  });

  const updateFund = useMutation({
    mutationFn: async (data: Record<string, unknown>) => {
      const response = await apiClient.patch<Fund>(`/api/funds/${fundId}`, data);
      return response.data;
    },
    onSuccess: (updatedFund) => {
      queryClient.setQueryData(['funds', 'detail', fundId], updatedFund);
      queryClient.invalidateQueries({ queryKey: ['funds', 'list'] });
      setEditModalVisible(false);
    },
    onError: (error: Error) => {
      Alert.alert('Error', error.message ?? 'Failed to update description');
    },
  });

  const activateFund = useMutation({
    mutationFn: async () => {
      const response = await apiClient.post<Fund>(`/api/funds/${fundId}/activate`);
      return response.data;
    },
    onSuccess: (updatedFund) => {
      queryClient.setQueryData(['funds', 'detail', fundId], updatedFund);
      queryClient.invalidateQueries({ queryKey: ['funds', 'list'] });
      queryClient.invalidateQueries({ queryKey: ['funds', 'dashboard', fundId] });
    },
    onError: (error: Error) => {
      Alert.alert('Error', error.message ?? 'Failed to activate fund');
    },
  });

  useEffect(() => {
    if (fund) {
      navigation.setOptions({ title: fund.name });
    }
  }, [fund?.name, navigation]);

  const openEditModal = () => {
    if (fund?.state === 'Draft') {
      setEditConfigModalVisible(true);
    } else {
      setEditDescription(fund?.description ?? '');
      setEditModalVisible(true);
    }
  };

  const handleSaveDescription = () => {
    const trimmed = editDescription.trim();
    updateFund.mutate({ description: trimmed || undefined });
  };

  const handleActivate = () => {
    Alert.alert(
      'Activate Fund',
      'Activate this fund? Configuration will become immutable.',
      [
        { text: 'Cancel', style: 'cancel' },
        { text: 'Activate', style: 'default', onPress: () => activateFund.mutate() },
      ],
    );
  };

  const onRefresh = () => {
    refetchFund();
    refetchDashboard();
  };

  if (fundLoading || dashLoading) {
    return (
      <View style={styles.center}>
        <ActivityIndicator size="large" color="#1E40AF" />
      </View>
    );
  }

  if (!fund) {
    return (
      <View style={styles.center}>
        <Text style={styles.errorText}>Fund not found</Text>
      </View>
    );
  }

  const stateColors = STATE_COLORS[fund.state] ?? STATE_COLORS.Dissolved;

  return (
    <SafeAreaView style={styles.container} edges={['bottom']}>
      <ScrollView
        contentContainerStyle={styles.scrollContent}
        refreshControl={
          <RefreshControl refreshing={fundRefetching} onRefresh={onRefresh} tintColor="#1E40AF" />
        }
      >
        {/* Header */}
        <View style={styles.headerSection}>
          <View style={styles.headerRow}>
            <Text style={styles.fundName}>{fund.name}</Text>
            <View style={[styles.badge, { backgroundColor: stateColors.bg }]}>
              <Text style={[styles.badgeText, { color: stateColors.text }]}>{fund.state}</Text>
            </View>
          </View>

          <View style={styles.descriptionRow}>
            <Text style={fund.description ? styles.description : styles.descriptionEmpty}>
              {fund.description || 'No description'}
            </Text>
            {canManageFund && (
              <TouchableOpacity onPress={openEditModal} style={styles.editButton}>
                <Text style={styles.editButtonText}>Edit</Text>
              </TouchableOpacity>
            )}
          </View>

          {/* Action buttons */}
          <View style={styles.actionRow}>
            {canManageFund && fund.state === 'Draft' && (
              <TouchableOpacity
                onPress={handleActivate}
                disabled={activateFund.isPending}
                style={[styles.activateButton, activateFund.isPending && styles.buttonDisabled]}
              >
                {activateFund.isPending ? (
                  <ActivityIndicator size="small" color="#FFFFFF" />
                ) : (
                  <Text style={styles.activateButtonText}>Activate Fund</Text>
                )}
              </TouchableOpacity>
            )}
          </View>
        </View>

        {/* Dashboard Stats */}
        {dashboard && (
          <View style={styles.statsGrid}>
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
          </View>
        )}

        {/* Configuration */}
        <View style={styles.configSection}>
          <View style={{ flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center', marginBottom: 12 }}>
            <Text style={styles.sectionTitle}>Configuration</Text>
            {canManageFund && fund.state === 'Draft' && (
              <TouchableOpacity
                onPress={() => setEditConfigModalVisible(true)}
                style={{ paddingHorizontal: 10, paddingVertical: 4, backgroundColor: '#EFF6FF', borderRadius: 6 }}
              >
                <Text style={{ fontSize: 12, fontWeight: '600', color: '#1E40AF' }}>Edit</Text>
              </TouchableOpacity>
            )}
          </View>
          <ConfigItem label="Currency" value={fund.currency} />
          <ConfigItem label="Monthly Interest Rate" value={`${(fund.monthlyInterestRate * 100).toFixed(2)}%`} />
          <ConfigItem label="Min Monthly Contribution" value={formatCurrency(fund.minimumMonthlyContribution, fund.currency)} />
          <ConfigItem label="Min Principal / Repayment" value={formatCurrency(fund.minimumPrincipalPerRepayment, fund.currency)} />
          <ConfigItem label="Loan Approval" value={fund.loanApprovalPolicy} />
          <ConfigItem label="Max Loan / Member" value={fund.maxLoanPerMember != null ? formatCurrency(fund.maxLoanPerMember, fund.currency) : 'No limit'} />
          <ConfigItem label="Max Concurrent Loans" value={fund.maxConcurrentLoans?.toString() ?? 'No limit'} />
          <ConfigItem label="Penalty Type" value={fund.overduePenaltyType} />
          <ConfigItem label="Contribution Day" value={`Day ${fund.contributionDayOfMonth}`} />
          <ConfigItem label="Grace Period" value={`${fund.gracePeriodDays} days`} />
        </View>
      </ScrollView>

      {/* Edit Description Modal */}
      <Modal visible={editModalVisible} transparent animationType="fade">
        <View style={styles.modalBackdrop}>
          <View style={styles.modalContent}>
            <Text style={styles.modalTitle}>Edit Fund Description</Text>
            <TextInput
              style={styles.textInput}
              value={editDescription}
              onChangeText={setEditDescription}
              placeholder="Enter fund description (optional)"
              placeholderTextColor="#9CA3AF"
              multiline
              maxLength={500}
              autoFocus
            />
            <Text style={styles.charCount}>{editDescription.length}/500</Text>

            <View style={styles.modalActions}>
              <TouchableOpacity
                onPress={() => setEditModalVisible(false)}
                disabled={updateFund.isPending}
                style={styles.cancelButton}
              >
                <Text style={styles.cancelButtonText}>Cancel</Text>
              </TouchableOpacity>
              <TouchableOpacity
                onPress={handleSaveDescription}
                disabled={updateFund.isPending || editDescription.trim() === (fund.description ?? '')}
                style={[
                  styles.saveButton,
                  (updateFund.isPending || editDescription.trim() === (fund.description ?? '')) &&
                    styles.buttonDisabled,
                ]}
              >
                {updateFund.isPending ? (
                  <ActivityIndicator size="small" color="#FFFFFF" />
                ) : (
                  <Text style={styles.saveButtonText}>Save</Text>
                )}
              </TouchableOpacity>
            </View>
          </View>
        </View>
      </Modal>

      {/* Edit Configuration Modal (Draft only) */}
      {fund.state === 'Draft' && (
        <EditConfigModal
          fund={fund}
          visible={editConfigModalVisible}
          onClose={() => setEditConfigModalVisible(false)}
          onSave={(data) => {
            updateFund.mutate(data, {
              onSuccess: () => setEditConfigModalVisible(false),
            });
          }}
          isPending={updateFund.isPending}
        />
      )}
    </SafeAreaView>
  );
}

// ── Helper Components ──

function StatCard({ label, value, alert = false }: { label: string; value: string; alert?: boolean }) {
  return (
    <View style={[styles.statCard, alert && styles.statCardAlert]}>
      <Text style={styles.statLabel}>{label}</Text>
      <Text style={[styles.statValue, alert && styles.statValueAlert]}>{value}</Text>
    </View>
  );
}

function ConfigItem({ label, value }: { label: string; value: string }) {
  return (
    <View style={styles.configItem}>
      <Text style={styles.configLabel}>{label}</Text>
      <Text style={styles.configValue}>{value}</Text>
    </View>
  );
}

// ── Edit Config Modal (Draft only) ──

function EditConfigModal({
  fund,
  visible,
  onClose,
  onSave,
  isPending,
}: {
  fund: Fund;
  visible: boolean;
  onClose: () => void;
  onSave: (data: Record<string, unknown>) => void;
  isPending: boolean;
}) {
  const [form, setForm] = useState({
    name: fund.name,
    description: fund.description ?? '',
    monthlyInterestRate: (fund.monthlyInterestRate * 100).toFixed(2),
    minimumMonthlyContribution: fund.minimumMonthlyContribution.toString(),
    minimumPrincipalPerRepayment: fund.minimumPrincipalPerRepayment.toString(),
    loanApprovalPolicy: fund.loanApprovalPolicy,
    maxLoanPerMember: fund.maxLoanPerMember?.toString() ?? '',
    maxConcurrentLoans: fund.maxConcurrentLoans?.toString() ?? '',
    overduePenaltyType: fund.overduePenaltyType,
    overduePenaltyValue: fund.overduePenaltyValue.toString(),
    contributionDayOfMonth: fund.contributionDayOfMonth.toString(),
    gracePeriodDays: fund.gracePeriodDays.toString(),
  });

  useEffect(() => {
    if (visible) {
      setForm({
        name: fund.name,
        description: fund.description ?? '',
        monthlyInterestRate: (fund.monthlyInterestRate * 100).toFixed(2),
        minimumMonthlyContribution: fund.minimumMonthlyContribution.toString(),
        minimumPrincipalPerRepayment: fund.minimumPrincipalPerRepayment.toString(),
        loanApprovalPolicy: fund.loanApprovalPolicy,
        maxLoanPerMember: fund.maxLoanPerMember?.toString() ?? '',
        maxConcurrentLoans: fund.maxConcurrentLoans?.toString() ?? '',
        overduePenaltyType: fund.overduePenaltyType,
        overduePenaltyValue: fund.overduePenaltyValue.toString(),
        contributionDayOfMonth: fund.contributionDayOfMonth.toString(),
        gracePeriodDays: fund.gracePeriodDays.toString(),
      });
    }
  }, [visible, fund]);

  const handleSave = () => {
    const payload: Record<string, unknown> = {};
    let hasChanges = false;

    if (form.name.trim() !== fund.name) { payload.name = form.name.trim(); hasChanges = true; }
    if ((form.description.trim() || undefined) !== (fund.description ?? undefined)) { payload.description = form.description.trim() || undefined; hasChanges = true; }
    const rate = parseFloat(form.monthlyInterestRate) / 100;
    if (rate !== fund.monthlyInterestRate) { payload.monthlyInterestRate = rate; hasChanges = true; }
    if (parseFloat(form.minimumMonthlyContribution) !== fund.minimumMonthlyContribution) { payload.minimumMonthlyContribution = parseFloat(form.minimumMonthlyContribution); hasChanges = true; }
    if (parseFloat(form.minimumPrincipalPerRepayment) !== fund.minimumPrincipalPerRepayment) { payload.minimumPrincipalPerRepayment = parseFloat(form.minimumPrincipalPerRepayment); hasChanges = true; }
    if (form.loanApprovalPolicy !== fund.loanApprovalPolicy) { payload.loanApprovalPolicy = form.loanApprovalPolicy; hasChanges = true; }
    const maxLoan = form.maxLoanPerMember ? parseFloat(form.maxLoanPerMember) : null;
    if (maxLoan !== (fund.maxLoanPerMember ?? null)) {
      if (maxLoan === null) payload.clearMaxLoanPerMember = true; else payload.maxLoanPerMember = maxLoan;
      hasChanges = true;
    }
    const maxConc = form.maxConcurrentLoans ? parseInt(form.maxConcurrentLoans) : null;
    if (maxConc !== (fund.maxConcurrentLoans ?? null)) {
      if (maxConc === null) payload.clearMaxConcurrentLoans = true; else payload.maxConcurrentLoans = maxConc;
      hasChanges = true;
    }
    if (form.overduePenaltyType !== fund.overduePenaltyType) { payload.overduePenaltyType = form.overduePenaltyType; hasChanges = true; }
    if (parseFloat(form.overduePenaltyValue) !== fund.overduePenaltyValue) { payload.overduePenaltyValue = parseFloat(form.overduePenaltyValue); hasChanges = true; }
    if (parseInt(form.contributionDayOfMonth) !== fund.contributionDayOfMonth) { payload.contributionDayOfMonth = parseInt(form.contributionDayOfMonth); hasChanges = true; }
    if (parseInt(form.gracePeriodDays) !== fund.gracePeriodDays) { payload.gracePeriodDays = parseInt(form.gracePeriodDays); hasChanges = true; }

    if (!hasChanges) { onClose(); return; }
    onSave(payload);
  };

  const setField = (key: string, val: string) => setForm(prev => ({ ...prev, [key]: val }));

  return (
    <Modal visible={visible} transparent animationType="fade">
      <View style={styles.modalBackdrop}>
        <View style={[styles.modalContent, { maxHeight: '85%' }]}>
          <Text style={styles.modalTitle}>Edit Fund Configuration</Text>
          <Text style={{ fontSize: 11, color: '#D97706', marginBottom: 12 }}>
            Configuration is editable while fund is in Draft state.
          </Text>
          <ScrollView showsVerticalScrollIndicator={false}>
            <ConfigField label="Fund Name" value={form.name} onChangeText={v => setField('name', v)} />
            <ConfigField label="Description" value={form.description} onChangeText={v => setField('description', v)} multiline />
            <ConfigField label="Monthly Interest Rate (%)" value={form.monthlyInterestRate} onChangeText={v => setField('monthlyInterestRate', v)} keyboardType="decimal-pad" />
            <ConfigField label="Min Monthly Contribution" value={form.minimumMonthlyContribution} onChangeText={v => setField('minimumMonthlyContribution', v)} keyboardType="decimal-pad" />
            <ConfigField label="Min Principal / Repayment" value={form.minimumPrincipalPerRepayment} onChangeText={v => setField('minimumPrincipalPerRepayment', v)} keyboardType="decimal-pad" />
            <ConfigField label="Loan Approval" value={form.loanApprovalPolicy} onChangeText={v => setField('loanApprovalPolicy', v)} />
            <ConfigField label="Max Loan / Member" value={form.maxLoanPerMember} onChangeText={v => setField('maxLoanPerMember', v)} keyboardType="decimal-pad" placeholder="No limit" />
            <ConfigField label="Max Concurrent Loans" value={form.maxConcurrentLoans} onChangeText={v => setField('maxConcurrentLoans', v)} keyboardType="number-pad" placeholder="No limit" />
            <ConfigField label="Penalty Type" value={form.overduePenaltyType} onChangeText={v => setField('overduePenaltyType', v)} />
            <ConfigField label="Penalty Value" value={form.overduePenaltyValue} onChangeText={v => setField('overduePenaltyValue', v)} keyboardType="decimal-pad" />
            <ConfigField label="Contribution Day (1-28)" value={form.contributionDayOfMonth} onChangeText={v => setField('contributionDayOfMonth', v)} keyboardType="number-pad" />
            <ConfigField label="Grace Period (days)" value={form.gracePeriodDays} onChangeText={v => setField('gracePeriodDays', v)} keyboardType="number-pad" />
          </ScrollView>
          <View style={[styles.modalActions, { marginTop: 12 }]}>
            <TouchableOpacity onPress={onClose} disabled={isPending} style={styles.cancelButton}>
              <Text style={styles.cancelButtonText}>Cancel</Text>
            </TouchableOpacity>
            <TouchableOpacity onPress={handleSave} disabled={isPending} style={[styles.saveButton, isPending && styles.buttonDisabled]}>
              {isPending ? <ActivityIndicator size="small" color="#FFFFFF" /> : <Text style={styles.saveButtonText}>Save</Text>}
            </TouchableOpacity>
          </View>
        </View>
      </View>
    </Modal>
  );
}

function ConfigField({
  label, value, onChangeText, multiline, keyboardType, placeholder,
}: {
  label: string; value: string; onChangeText: (v: string) => void;
  multiline?: boolean; keyboardType?: 'default' | 'decimal-pad' | 'number-pad'; placeholder?: string;
}) {
  return (
    <View style={{ marginBottom: 10 }}>
      <Text style={{ fontSize: 12, color: '#6B7280', marginBottom: 4 }}>{label}</Text>
      <TextInput
        value={value}
        onChangeText={onChangeText}
        style={[styles.textInput, multiline && { minHeight: 60 }]}
        multiline={multiline}
        keyboardType={keyboardType ?? 'default'}
        placeholder={placeholder}
        placeholderTextColor="#9CA3AF"
      />
    </View>
  );
}

// ── Styles ──

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#F9FAFB',
  },
  scrollContent: {
    padding: 16,
    paddingBottom: 32,
  },
  center: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
  },
  errorText: {
    fontSize: 14,
    color: '#DC2626',
  },

  // Header
  headerSection: {
    backgroundColor: '#FFFFFF',
    borderRadius: 12,
    padding: 16,
    marginBottom: 16,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 1 },
    shadowOpacity: 0.05,
    shadowRadius: 3,
    elevation: 2,
  },
  headerRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: 8,
  },
  fundName: {
    fontSize: 20,
    fontWeight: 'bold',
    color: '#111827',
    flex: 1,
    marginRight: 8,
  },
  badge: {
    paddingHorizontal: 10,
    paddingVertical: 3,
    borderRadius: 12,
  },
  badgeText: {
    fontSize: 12,
    fontWeight: '600',
  },
  descriptionRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
    marginBottom: 12,
  },
  description: {
    fontSize: 14,
    color: '#6B7280',
    flex: 1,
  },
  descriptionEmpty: {
    fontSize: 14,
    color: '#9CA3AF',
    fontStyle: 'italic',
    flex: 1,
  },
  editButton: {
    paddingHorizontal: 10,
    paddingVertical: 4,
    backgroundColor: '#EFF6FF',
    borderRadius: 6,
  },
  editButtonText: {
    fontSize: 12,
    fontWeight: '600',
    color: '#1E40AF',
  },
  actionRow: {
    flexDirection: 'row',
    gap: 8,
  },
  activateButton: {
    paddingHorizontal: 16,
    paddingVertical: 8,
    backgroundColor: '#059669',
    borderRadius: 8,
    alignItems: 'center',
    minWidth: 120,
  },
  activateButtonText: {
    color: '#FFFFFF',
    fontWeight: '600',
    fontSize: 14,
  },
  buttonDisabled: {
    opacity: 0.5,
  },

  // Stats
  statsGrid: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: 10,
    marginBottom: 16,
  },
  statCard: {
    backgroundColor: '#FFFFFF',
    borderRadius: 10,
    padding: 12,
    width: '48%' as any,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 1 },
    shadowOpacity: 0.05,
    shadowRadius: 2,
    elevation: 1,
  },
  statCardAlert: {
    backgroundColor: '#FEF2F2',
    borderWidth: 1,
    borderColor: '#FECACA',
  },
  statLabel: {
    fontSize: 11,
    fontWeight: '500',
    color: '#6B7280',
    textTransform: 'uppercase',
    marginBottom: 4,
  },
  statValue: {
    fontSize: 18,
    fontWeight: 'bold',
    color: '#111827',
  },
  statValueAlert: {
    color: '#DC2626',
  },

  // Config
  configSection: {
    backgroundColor: '#FFFFFF',
    borderRadius: 12,
    padding: 16,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 1 },
    shadowOpacity: 0.05,
    shadowRadius: 3,
    elevation: 2,
  },
  sectionTitle: {
    fontSize: 16,
    fontWeight: '600',
    color: '#111827',
    marginBottom: 12,
  },
  configItem: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    paddingVertical: 8,
    borderBottomWidth: StyleSheet.hairlineWidth,
    borderBottomColor: '#E5E7EB',
  },
  configLabel: {
    fontSize: 13,
    color: '#6B7280',
  },
  configValue: {
    fontSize: 13,
    fontWeight: '500',
    color: '#111827',
  },

  // Modal
  modalBackdrop: {
    flex: 1,
    backgroundColor: 'rgba(0,0,0,0.4)',
    justifyContent: 'center',
    alignItems: 'center',
    padding: 24,
  },
  modalContent: {
    backgroundColor: '#FFFFFF',
    borderRadius: 16,
    padding: 20,
    width: '100%',
    maxWidth: 400,
  },
  modalTitle: {
    fontSize: 18,
    fontWeight: '600',
    color: '#111827',
    marginBottom: 12,
  },
  textInput: {
    borderWidth: 1,
    borderColor: '#D1D5DB',
    borderRadius: 8,
    paddingHorizontal: 12,
    paddingVertical: 10,
    fontSize: 14,
    color: '#111827',
    minHeight: 100,
    textAlignVertical: 'top',
  },
  charCount: {
    fontSize: 11,
    color: '#9CA3AF',
    textAlign: 'right',
    marginTop: 4,
  },
  modalActions: {
    flexDirection: 'row',
    justifyContent: 'flex-end',
    gap: 10,
    marginTop: 16,
  },
  cancelButton: {
    paddingHorizontal: 16,
    paddingVertical: 8,
    backgroundColor: '#F3F4F6',
    borderRadius: 8,
  },
  cancelButtonText: {
    fontSize: 14,
    fontWeight: '500',
    color: '#374151',
  },
  saveButton: {
    paddingHorizontal: 16,
    paddingVertical: 8,
    backgroundColor: '#1E40AF',
    borderRadius: 8,
    minWidth: 70,
    alignItems: 'center',
  },
  saveButtonText: {
    fontSize: 14,
    fontWeight: '600',
    color: '#FFFFFF',
  },
});
