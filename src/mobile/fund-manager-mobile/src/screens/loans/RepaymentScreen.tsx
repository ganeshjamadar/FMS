import React, { useState } from 'react';
import {
  View,
  Text,
  FlatList,
  TouchableOpacity,
  ActivityIndicator,
  StyleSheet,
  TextInput,
  Modal,
  Alert,
} from 'react-native';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import apiClient from '../../services/apiClient';
import { usePermissions } from '../../hooks/usePermissions';

interface RepaymentEntry {
  id: string;
  loanId: string;
  monthYear: number;
  interestDue: number;
  principalDue: number;
  totalDue: number;
  amountPaid: number;
  status: string;
  dueDate: string;
  paidDate?: string;
  version: string;
}

interface Loan {
  id: string;
  principalAmount: number;
  outstandingPrincipal: number;
  monthlyInterestRate: number;
  scheduledInstallment: number;
  status: string;
}

interface Props {
  route: { params: { fundId: string; loanId: string } };
  navigation: any;
}

const statusColors: Record<string, { bg: string; text: string }> = {
  Pending: { bg: '#fef3c7', text: '#92400e' },
  Paid: { bg: '#d1fae5', text: '#065f46' },
  Partial: { bg: '#dbeafe', text: '#1e40af' },
  Overdue: { bg: '#fee2e2', text: '#991b1b' },
};

export default function RepaymentScreen({ route, navigation }: Props) {
  const { fundId, loanId } = route.params;
  const queryClient = useQueryClient();
  const { canManageFund, canWrite } = usePermissions(fundId);

  const [payEntry, setPayEntry] = useState<RepaymentEntry | null>(null);
  const [payAmount, setPayAmount] = useState('');
  const [genMonth, setGenMonth] = useState('');

  // Fetch loan details
  const { data: loan } = useQuery({
    queryKey: ['loan', fundId, loanId],
    queryFn: async () => {
      const { data } = await apiClient.get<Loan>(`/api/funds/${fundId}/loans/${loanId}`);
      return data;
    },
  });

  // Fetch repayment entries
  const { data: repayments, isLoading, refetch } = useQuery({
    queryKey: ['repayments', fundId, loanId],
    queryFn: async () => {
      const { data } = await apiClient.get<RepaymentEntry[]>(
        `/api/funds/${fundId}/loans/${loanId}/repayments`
      );
      return data;
    },
  });

  // Generate repayment mutation
  const generateMutation = useMutation({
    mutationFn: async (monthYear: number) => {
      const { data } = await apiClient.post<RepaymentEntry>(
        `/api/funds/${fundId}/loans/${loanId}/repayments/generate`,
        { monthYear }
      );
      return data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['repayments', fundId, loanId] });
      setGenMonth('');
    },
    onError: () => Alert.alert('Error', 'Failed to generate repayment entry.'),
  });

  // Record repayment mutation
  const recordMutation = useMutation({
    mutationFn: async ({ entryId, amount, version }: { entryId: string; amount: number; version: string }) => {
      const { data } = await apiClient.post(
        `/api/funds/${fundId}/loans/${loanId}/repayments/${entryId}/pay`,
        { amount },
        {
          headers: {
            'Idempotency-Key': `${Date.now()}-${Math.random().toString(36).slice(2)}`,
            'If-Match': version,
          },
        }
      );
      return data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['repayments', fundId, loanId] });
      queryClient.invalidateQueries({ queryKey: ['loan', fundId, loanId] });
      setPayEntry(null);
      setPayAmount('');
    },
    onError: () => Alert.alert('Error', 'Failed to record payment.'),
  });

  const handleGenerate = () => {
    const cleaned = genMonth.replace(/[^0-9]/g, '');
    const monthYear = parseInt(cleaned, 10);
    if (isNaN(monthYear) || cleaned.length !== 6) {
      Alert.alert('Invalid', 'Enter month in YYYYMM format (e.g., 202501).');
      return;
    }
    generateMutation.mutate(monthYear);
  };

  const handlePay = () => {
    if (!payEntry) return;
    const amount = parseFloat(payAmount);
    if (isNaN(amount) || amount <= 0) {
      Alert.alert('Invalid', 'Enter a valid payment amount.');
      return;
    }
    recordMutation.mutate({ entryId: payEntry.id, amount, version: payEntry.version });
  };

  const formatMonth = (ym: number) => {
    const y = Math.floor(ym / 100);
    const m = ym % 100;
    return `${y}-${String(m).padStart(2, '0')}`;
  };

  if (isLoading) {
    return (
      <View style={styles.center}>
        <ActivityIndicator size="large" color="#1E40AF" />
      </View>
    );
  }

  return (
    <View style={styles.container}>
      {/* Loan Summary */}
      {loan && (
        <View style={styles.summaryRow}>
          <View style={styles.summaryCard}>
            <Text style={styles.summaryLabel}>Outstanding</Text>
            <Text style={styles.summaryValue}>{loan.outstandingPrincipal.toLocaleString()}</Text>
          </View>
          <View style={styles.summaryCard}>
            <Text style={styles.summaryLabel}>Rate</Text>
            <Text style={styles.summaryValue}>{(loan.monthlyInterestRate * 100).toFixed(2)}%</Text>
          </View>
          <View style={styles.summaryCard}>
            <Text style={styles.summaryLabel}>Installment</Text>
            <Text style={styles.summaryValue}>{loan.scheduledInstallment.toLocaleString()}</Text>
          </View>
        </View>
      )}

      {/* Generate Section */}
      {loan?.status === 'Active' && canManageFund && (
        <View style={styles.generateRow}>
          <TextInput
            style={styles.genInput}
            placeholder="YYYYMM"
            keyboardType="numeric"
            maxLength={6}
            value={genMonth}
            onChangeText={setGenMonth}
          />
          <TouchableOpacity
            style={[styles.genButton, generateMutation.isPending && styles.disabledButton]}
            onPress={handleGenerate}
            disabled={generateMutation.isPending}
          >
            <Text style={styles.genButtonText}>
              {generateMutation.isPending ? '...' : 'Generate'}
            </Text>
          </TouchableOpacity>
        </View>
      )}

      {/* Repayment List */}
      <FlatList
        data={repayments ?? []}
        keyExtractor={(item) => item.id}
        refreshing={isLoading}
        onRefresh={refetch}
        ListEmptyComponent={
          <Text style={styles.emptyText}>No repayment entries yet.</Text>
        }
        renderItem={({ item }) => {
          const colors = statusColors[item.status] || { bg: '#f3f4f6', text: '#374151' };
          const remaining = item.totalDue - item.amountPaid;
          const canPay = canWrite && item.status !== 'Paid';

          return (
            <View style={styles.card}>
              <View style={styles.cardHeader}>
                <Text style={styles.monthText}>{formatMonth(item.monthYear)}</Text>
                <View style={[styles.badge, { backgroundColor: colors.bg }]}>
                  <Text style={[styles.badgeText, { color: colors.text }]}>{item.status}</Text>
                </View>
              </View>

              <View style={styles.cardRow}>
                <View style={styles.cardCol}>
                  <Text style={styles.cardLabel}>Interest</Text>
                  <Text style={styles.cardAmount}>{item.interestDue.toLocaleString()}</Text>
                </View>
                <View style={styles.cardCol}>
                  <Text style={styles.cardLabel}>Principal</Text>
                  <Text style={styles.cardAmount}>{item.principalDue.toLocaleString()}</Text>
                </View>
                <View style={styles.cardCol}>
                  <Text style={styles.cardLabel}>Total</Text>
                  <Text style={[styles.cardAmount, styles.totalAmount]}>{item.totalDue.toLocaleString()}</Text>
                </View>
              </View>

              <View style={styles.cardRow}>
                <View style={styles.cardCol}>
                  <Text style={styles.cardLabel}>Paid</Text>
                  <Text style={[styles.cardAmount, { color: '#065f46' }]}>{item.amountPaid.toLocaleString()}</Text>
                </View>
                <View style={styles.cardCol}>
                  <Text style={styles.cardLabel}>Remaining</Text>
                  <Text style={[styles.cardAmount, { color: remaining > 0 ? '#dc2626' : '#065f46' }]}>
                    {remaining.toLocaleString()}
                  </Text>
                </View>
                <View style={styles.cardCol}>
                  <Text style={styles.cardLabel}>Due</Text>
                  <Text style={styles.cardAmount}>{item.dueDate}</Text>
                </View>
              </View>

              {canPay && (
                <TouchableOpacity
                  style={styles.payButton}
                  onPress={() => {
                    setPayEntry(item);
                    setPayAmount(String(remaining));
                  }}
                >
                  <Text style={styles.payButtonText}>Record Payment</Text>
                </TouchableOpacity>
              )}
            </View>
          );
        }}
      />

      {/* Payment Modal */}
      <Modal visible={!!payEntry} transparent animationType="slide">
        <View style={styles.modalOverlay}>
          <View style={styles.modalContent}>
            <Text style={styles.modalTitle}>Record Payment</Text>
            {payEntry && (
              <Text style={styles.modalSubtitle}>
                Month: {formatMonth(payEntry.monthYear)} | Remaining: {(payEntry.totalDue - payEntry.amountPaid).toLocaleString()}
              </Text>
            )}
            <TextInput
              style={styles.modalInput}
              placeholder="Payment Amount"
              keyboardType="decimal-pad"
              value={payAmount}
              onChangeText={setPayAmount}
            />
            <View style={styles.modalButtons}>
              <TouchableOpacity
                style={styles.cancelButton}
                onPress={() => { setPayEntry(null); setPayAmount(''); }}
              >
                <Text style={styles.cancelButtonText}>Cancel</Text>
              </TouchableOpacity>
              <TouchableOpacity
                style={[styles.confirmButton, recordMutation.isPending && styles.disabledButton]}
                onPress={handlePay}
                disabled={recordMutation.isPending}
              >
                <Text style={styles.confirmButtonText}>
                  {recordMutation.isPending ? 'Recording...' : 'Pay'}
                </Text>
              </TouchableOpacity>
            </View>
          </View>
        </View>
      </Modal>
    </View>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: '#f9fafb' },
  center: { flex: 1, justifyContent: 'center', alignItems: 'center' },

  summaryRow: { flexDirection: 'row', padding: 12, gap: 8 },
  summaryCard: { flex: 1, backgroundColor: '#fff', borderRadius: 8, padding: 12, elevation: 1 },
  summaryLabel: { fontSize: 11, color: '#6b7280', marginBottom: 2 },
  summaryValue: { fontSize: 16, fontWeight: '700', color: '#111827' },

  generateRow: { flexDirection: 'row', paddingHorizontal: 12, paddingBottom: 12, gap: 8 },
  genInput: { flex: 1, borderWidth: 1, borderColor: '#d1d5db', borderRadius: 8, paddingHorizontal: 12, paddingVertical: 8, backgroundColor: '#fff' },
  genButton: { backgroundColor: '#1E40AF', borderRadius: 8, paddingHorizontal: 16, justifyContent: 'center' },
  genButtonText: { color: '#fff', fontWeight: '600' },

  emptyText: { textAlign: 'center', color: '#9ca3af', marginTop: 40, fontSize: 14 },

  card: { marginHorizontal: 12, marginBottom: 10, backgroundColor: '#fff', borderRadius: 10, padding: 14, elevation: 1 },
  cardHeader: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center', marginBottom: 10 },
  monthText: { fontSize: 16, fontWeight: '700', color: '#111827' },
  badge: { paddingHorizontal: 8, paddingVertical: 3, borderRadius: 12 },
  badgeText: { fontSize: 11, fontWeight: '600' },

  cardRow: { flexDirection: 'row', marginBottom: 6 },
  cardCol: { flex: 1 },
  cardLabel: { fontSize: 11, color: '#6b7280', marginBottom: 1 },
  cardAmount: { fontSize: 14, fontWeight: '600', color: '#111827' },
  totalAmount: { color: '#1E40AF' },

  payButton: { marginTop: 8, backgroundColor: '#059669', borderRadius: 8, paddingVertical: 10, alignItems: 'center' },
  payButtonText: { color: '#fff', fontWeight: '600', fontSize: 14 },

  disabledButton: { opacity: 0.5 },

  modalOverlay: { flex: 1, backgroundColor: 'rgba(0,0,0,0.4)', justifyContent: 'center', paddingHorizontal: 24 },
  modalContent: { backgroundColor: '#fff', borderRadius: 12, padding: 20 },
  modalTitle: { fontSize: 18, fontWeight: '700', marginBottom: 4 },
  modalSubtitle: { fontSize: 13, color: '#6b7280', marginBottom: 16 },
  modalInput: { borderWidth: 1, borderColor: '#d1d5db', borderRadius: 8, paddingHorizontal: 12, paddingVertical: 10, fontSize: 16, marginBottom: 16 },
  modalButtons: { flexDirection: 'row', justifyContent: 'flex-end', gap: 12 },
  cancelButton: { paddingHorizontal: 16, paddingVertical: 10, borderRadius: 8, borderWidth: 1, borderColor: '#d1d5db' },
  cancelButtonText: { color: '#374151', fontWeight: '600' },
  confirmButton: { paddingHorizontal: 16, paddingVertical: 10, borderRadius: 8, backgroundColor: '#059669' },
  confirmButtonText: { color: '#fff', fontWeight: '600' },
});
