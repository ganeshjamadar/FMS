import React, { useState } from 'react';
import {
  View,
  Text,
  StyleSheet,
  ScrollView,
  TouchableOpacity,
  ActivityIndicator,
  Alert,
  Share,
  Platform,
} from 'react-native';
import { useQuery } from '@tanstack/react-query';
import axios from 'axios';
import type { NativeStackScreenProps } from '@react-navigation/native-stack';
import type { RootStackParamList } from '../../navigation/RootNavigator';
import { usePermissions } from '../../hooks/usePermissions';

type Props = NativeStackScreenProps<RootStackParamList, 'Reports'>;

const api = axios.create({ baseURL: '/api' });

type ReportType = 'contribution-summary' | 'loan-portfolio' | 'interest-earnings' | 'balance-sheet';

const REPORT_OPTIONS: { value: ReportType; label: string; needsDates: boolean }[] = [
  { value: 'contribution-summary', label: 'Contribution Summary', needsDates: true },
  { value: 'loan-portfolio', label: 'Loan Portfolio', needsDates: false },
  { value: 'interest-earnings', label: 'Interest Earnings', needsDates: true },
  { value: 'balance-sheet', label: 'Balance Sheet', needsDates: true },
];

function getCurrentYYYYMM(): number {
  const d = new Date();
  return d.getFullYear() * 100 + (d.getMonth() + 1);
}

function getYearStartYYYYMM(): number {
  return new Date().getFullYear() * 100 + 1;
}

export default function ReportsScreen({ route }: Props) {
  const { fundId } = route.params;
  const { canExport } = usePermissions(fundId);
  const [selectedReport, setSelectedReport] = useState<ReportType>('contribution-summary');
  const [fromMonth] = useState(getYearStartYYYYMM());
  const [toMonth] = useState(getCurrentYYYYMM());

  const needsDates = REPORT_OPTIONS.find(o => o.value === selectedReport)?.needsDates ?? false;

  const { data, isLoading, error } = useQuery({
    queryKey: ['reports', fundId, selectedReport, fromMonth, toMonth],
    queryFn: async () => {
      const params: Record<string, string | number> = {};
      if (needsDates) {
        params.fromMonth = fromMonth;
        params.toMonth = toMonth;
      }
      const { data } = await api.get(`/funds/${fundId}/reports/${selectedReport}`, { params });
      return data;
    },
    enabled: !!fundId,
  });

  const handleShare = async (format: 'csv' | 'pdf') => {
    try {
      const params: Record<string, string | number> = { format };
      if (needsDates) {
        params.fromMonth = fromMonth;
        params.toMonth = toMonth;
      }
      const queryStr = new URLSearchParams(
        Object.entries(params).reduce((a, [k, v]) => ({ ...a, [k]: String(v) }), {} as Record<string, string>)
      ).toString();
      const url = `/api/funds/${fundId}/reports/${selectedReport}?${queryStr}`;

      if (Platform.OS === 'web') {
        // For web, trigger download
        window.open(url, '_blank');
      } else {
        await Share.share({
          message: `Download ${selectedReport} report: ${url}`,
          title: `${selectedReport} Report`,
        });
      }
    } catch (err) {
      Alert.alert('Error', 'Failed to share report');
    }
  };

  return (
    <ScrollView style={styles.container}>
      <Text style={styles.title}>Reports &amp; Exports</Text>

      {/* Report Selector */}
      <View style={styles.selectorContainer}>
        {REPORT_OPTIONS.map(opt => (
          <TouchableOpacity
            key={opt.value}
            style={[styles.selectorButton, selectedReport === opt.value && styles.selectorButtonActive]}
            onPress={() => setSelectedReport(opt.value)}
          >
            <Text style={[styles.selectorText, selectedReport === opt.value && styles.selectorTextActive]}>
              {opt.label}
            </Text>
          </TouchableOpacity>
        ))}
      </View>

      {/* Date Range Info */}
      {needsDates && (
        <View style={styles.dateInfo}>
          <Text style={styles.dateLabel}>
            Period: {fromMonth} - {toMonth}
          </Text>
        </View>
      )}

      {/* Export Buttons */}
      {canExport && (
      <View style={styles.exportRow}>
        <TouchableOpacity style={styles.csvButton} onPress={() => handleShare('csv')}>
          <Text style={styles.exportButtonText}>Export CSV</Text>
        </TouchableOpacity>
        <TouchableOpacity style={styles.pdfButton} onPress={() => handleShare('pdf')}>
          <Text style={styles.exportButtonText}>Export PDF</Text>
        </TouchableOpacity>
      </View>
      )}

      {/* Loading */}
      {isLoading && <ActivityIndicator size="large" color="#1E40AF" style={styles.loader} />}

      {/* Error */}
      {error && (
        <View style={styles.errorCard}>
          <Text style={styles.errorText}>Failed to load report data</Text>
        </View>
      )}

      {/* Contribution Summary */}
      {selectedReport === 'contribution-summary' && data && (
        <View style={styles.card}>
          <Text style={styles.cardTitle}>Contribution Summary</Text>
          <View style={styles.statsRow}>
            <View style={styles.stat}>
              <Text style={[styles.statValue, { color: '#2563EB' }]}>{data.totalDue?.toFixed(2)}</Text>
              <Text style={styles.statLabel}>Total Due</Text>
            </View>
            <View style={styles.stat}>
              <Text style={[styles.statValue, { color: '#16A34A' }]}>{data.totalCollected?.toFixed(2)}</Text>
              <Text style={styles.statLabel}>Collected</Text>
            </View>
            <View style={styles.stat}>
              <Text style={[styles.statValue, { color: '#DC2626' }]}>{data.totalOutstanding?.toFixed(2)}</Text>
              <Text style={styles.statLabel}>Outstanding</Text>
            </View>
          </View>
          {data.members?.map((m: any) => (
            <View key={m.userId} style={styles.memberRow}>
              <Text style={styles.memberName}>{m.name}</Text>
              {m.months?.map((mo: any) => (
                <View key={mo.monthYear} style={styles.monthRow}>
                  <Text style={styles.monthText}>{mo.monthYear}</Text>
                  <Text style={styles.monthAmount}>{mo.amountPaid?.toFixed(2)} / {mo.amountDue?.toFixed(2)}</Text>
                  <Text style={[styles.badge, mo.status === 'Paid' ? styles.badgeGreen : styles.badgeRed]}>
                    {mo.status}
                  </Text>
                </View>
              ))}
            </View>
          ))}
        </View>
      )}

      {/* Loan Portfolio */}
      {selectedReport === 'loan-portfolio' && data && (
        <View style={styles.card}>
          <Text style={styles.cardTitle}>Loan Portfolio</Text>
          <View style={styles.statsRow}>
            <View style={styles.stat}>
              <Text style={[styles.statValue, { color: '#2563EB' }]}>{data.totalActiveLoans}</Text>
              <Text style={styles.statLabel}>Active</Text>
            </View>
            <View style={styles.stat}>
              <Text style={[styles.statValue, { color: '#EA580C' }]}>
                {data.totalOutstandingPrincipal?.toFixed(2)}
              </Text>
              <Text style={styles.statLabel}>Outstanding</Text>
            </View>
          </View>
          {data.loans?.map((l: any) => (
            <View key={l.loanId} style={styles.loanRow}>
              <View style={styles.loanHeader}>
                <Text style={styles.loanBorrower}>{l.borrowerName}</Text>
                <Text style={[styles.badge, l.status === 'Disbursed' ? styles.badgeGreen : styles.badgeYellow]}>
                  {l.status}
                </Text>
              </View>
              <Text style={styles.loanDetail}>
                Principal: {l.principalAmount?.toFixed(2)} | Outstanding: {l.outstandingPrincipal?.toFixed(2)}
              </Text>
            </View>
          ))}
        </View>
      )}

      {/* Interest Earnings */}
      {selectedReport === 'interest-earnings' && data && (
        <View style={styles.card}>
          <Text style={styles.cardTitle}>Interest Earnings</Text>
          <Text style={styles.totalText}>
            Total: {data.totalInterestEarned?.toFixed(2)}
          </Text>
          {data.months?.map((m: any) => (
            <View key={m.monthYear} style={styles.monthRow}>
              <Text style={styles.monthText}>{m.monthYear}</Text>
              <Text style={styles.monthAmount}>{m.interestEarned?.toFixed(2)}</Text>
              <Text style={styles.smallText}>{m.loanCount} loans</Text>
            </View>
          ))}
        </View>
      )}

      {/* Balance Sheet */}
      {selectedReport === 'balance-sheet' && data && (
        <View style={styles.card}>
          <Text style={styles.cardTitle}>Balance Sheet</Text>
          {[
            { label: 'Opening Balance', value: data.openingBalance },
            { label: 'Contributions', value: data.contributionsReceived, color: '#16A34A' },
            { label: 'Disbursements', value: data.disbursements, color: '#DC2626' },
            { label: 'Interest Earned', value: data.interestEarned, color: '#16A34A' },
            { label: 'Repayments', value: data.repaymentsReceived, color: '#16A34A' },
            { label: 'Penalties', value: data.penalties },
          ].map(item => (
            <View key={item.label} style={styles.balanceRow}>
              <Text style={styles.balanceLabel}>{item.label}</Text>
              <Text style={[styles.balanceValue, item.color ? { color: item.color } : {}]}>
                {item.value?.toFixed(2)}
              </Text>
            </View>
          ))}
          <View style={[styles.balanceRow, styles.balanceTotal]}>
            <Text style={styles.balanceTotalLabel}>Closing Balance</Text>
            <Text style={styles.balanceTotalValue}>{data.closingBalance?.toFixed(2)}</Text>
          </View>
        </View>
      )}
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: '#F3F4F6', padding: 16 },
  title: { fontSize: 24, fontWeight: 'bold', color: '#111827', marginBottom: 16 },
  selectorContainer: { flexDirection: 'row', flexWrap: 'wrap', gap: 8, marginBottom: 16 },
  selectorButton: {
    paddingHorizontal: 14, paddingVertical: 8,
    backgroundColor: '#E5E7EB', borderRadius: 8,
  },
  selectorButtonActive: { backgroundColor: '#1E40AF' },
  selectorText: { fontSize: 13, color: '#374151' },
  selectorTextActive: { color: '#FFFFFF', fontWeight: '600' },
  dateInfo: { marginBottom: 12 },
  dateLabel: { fontSize: 13, color: '#6B7280' },
  exportRow: { flexDirection: 'row', gap: 12, marginBottom: 16 },
  csvButton: { flex: 1, backgroundColor: '#16A34A', padding: 12, borderRadius: 8, alignItems: 'center' },
  pdfButton: { flex: 1, backgroundColor: '#DC2626', padding: 12, borderRadius: 8, alignItems: 'center' },
  exportButtonText: { color: '#FFFFFF', fontWeight: '600', fontSize: 14 },
  loader: { marginTop: 32 },
  errorCard: { backgroundColor: '#FEE2E2', padding: 16, borderRadius: 8 },
  errorText: { color: '#991B1B', fontSize: 14 },
  card: { backgroundColor: '#FFFFFF', borderRadius: 12, padding: 16, marginBottom: 16 },
  cardTitle: { fontSize: 18, fontWeight: '600', color: '#111827', marginBottom: 12 },
  statsRow: { flexDirection: 'row', justifyContent: 'space-around', marginBottom: 16 },
  stat: { alignItems: 'center' },
  statValue: { fontSize: 20, fontWeight: 'bold' },
  statLabel: { fontSize: 12, color: '#6B7280', marginTop: 2 },
  memberRow: { borderTopWidth: 1, borderTopColor: '#E5E7EB', paddingTop: 8, marginTop: 8 },
  memberName: { fontSize: 14, fontWeight: '600', color: '#111827', marginBottom: 4 },
  monthRow: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', paddingVertical: 4 },
  monthText: { fontSize: 13, color: '#374151', width: 80 },
  monthAmount: { fontSize: 13, color: '#374151' },
  badge: { fontSize: 11, paddingHorizontal: 8, paddingVertical: 2, borderRadius: 10, overflow: 'hidden' },
  badgeGreen: { backgroundColor: '#D1FAE5', color: '#065F46' },
  badgeRed: { backgroundColor: '#FEE2E2', color: '#991B1B' },
  badgeYellow: { backgroundColor: '#FEF3C7', color: '#92400E' },
  loanRow: { borderTopWidth: 1, borderTopColor: '#E5E7EB', paddingTop: 8, marginTop: 8 },
  loanHeader: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center' },
  loanBorrower: { fontSize: 14, fontWeight: '600', color: '#111827' },
  loanDetail: { fontSize: 12, color: '#6B7280', marginTop: 4 },
  totalText: { fontSize: 16, fontWeight: '600', color: '#2563EB', marginBottom: 12 },
  smallText: { fontSize: 12, color: '#6B7280' },
  balanceRow: {
    flexDirection: 'row', justifyContent: 'space-between',
    paddingVertical: 8, borderBottomWidth: 1, borderBottomColor: '#E5E7EB',
  },
  balanceLabel: { fontSize: 14, color: '#374151' },
  balanceValue: { fontSize: 14, fontWeight: '500', color: '#111827' },
  balanceTotal: { borderTopWidth: 2, borderTopColor: '#111827', borderBottomWidth: 0, marginTop: 4 },
  balanceTotalLabel: { fontSize: 15, fontWeight: 'bold', color: '#111827' },
  balanceTotalValue: { fontSize: 15, fontWeight: 'bold', color: '#2563EB' },
});
