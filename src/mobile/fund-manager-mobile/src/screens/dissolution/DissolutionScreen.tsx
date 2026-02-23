import React from 'react';
import {
  View,
  Text,
  StyleSheet,
  ScrollView,
  TouchableOpacity,
  ActivityIndicator,
  Alert,
  FlatList,
} from 'react-native';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import type { NativeStackScreenProps } from '@react-navigation/native-stack';
import type { RootStackParamList } from '../../navigation/RootNavigator';
import apiClient from '../../services/apiClient';
import { usePermissions } from '../../hooks/usePermissions';

interface DissolutionSettlement {
  id: string;
  fundId: string;
  totalInterestPool: number;
  totalContributionsCollected: number;
  status: string;
  settlementDate?: string;
  confirmedBy?: string;
  createdAt: string;
}

interface DissolutionLineItem {
  userId: string;
  userName?: string;
  totalPaidContributions: number;
  interestShare: number;
  outstandingLoanPrincipal: number;
  unpaidInterest: number;
  unpaidDues: number;
  grossPayout: number;
  netPayout: number;
}

interface DissolutionBlocker {
  userId: string;
  userName?: string;
  netPayout: number;
  outstandingAmount: number;
}

interface SettlementDetail {
  settlement: DissolutionSettlement;
  lineItems: DissolutionLineItem[];
  canConfirm: boolean;
  blockers: DissolutionBlocker[];
}

type Props = NativeStackScreenProps<RootStackParamList, 'Dissolution'>;

export default function DissolutionScreen({ route }: Props) {
  const { fundId } = route.params;
  const qc = useQueryClient();
  const { canManageFund } = usePermissions(fundId);

  const settlementQuery = useQuery({
    queryKey: ['dissolution', fundId],
    queryFn: async () => {
      const { data } = await apiClient.get<SettlementDetail>(
        `/api/funds/${fundId}/dissolution/settlement`
      );
      return data;
    },
  });

  const initiate = useMutation({
    mutationFn: async () => {
      const { data } = await apiClient.post<DissolutionSettlement>(
        `/api/funds/${fundId}/dissolution/initiate`
      );
      return data;
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['dissolution', fundId] });
      Alert.alert('Success', 'Dissolution initiated.');
    },
    onError: () => Alert.alert('Error', 'Failed to initiate dissolution.'),
  });

  const recalculate = useMutation({
    mutationFn: async () => {
      const { data } = await apiClient.post<SettlementDetail>(
        `/api/funds/${fundId}/dissolution/settlement/recalculate`
      );
      return data;
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['dissolution', fundId] });
    },
    onError: () => Alert.alert('Error', 'Failed to recalculate.'),
  });

  const confirm = useMutation({
    mutationFn: async () => {
      const { data } = await apiClient.post<DissolutionSettlement>(
        `/api/funds/${fundId}/dissolution/confirm`
      );
      return data;
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['dissolution', fundId] });
      Alert.alert('Success', 'Dissolution confirmed. Fund is now dissolved.');
    },
    onError: () =>
      Alert.alert('Error', 'Failed to confirm. Some members may have negative net payout.'),
  });

  const detail = settlementQuery.data;
  const isConfirmed = detail?.settlement.status === 'Confirmed';

  const renderLineItem = ({ item }: { item: DissolutionLineItem }) => (
    <View style={styles.lineItem}>
      <Text style={styles.memberId}>{item.userName ?? item.userId.slice(0, 8)}</Text>
      <View style={styles.lineRow}>
        <Text style={styles.lineLabel}>Contributions:</Text>
        <Text style={styles.lineValue}>{item.totalPaidContributions.toFixed(2)}</Text>
      </View>
      <View style={styles.lineRow}>
        <Text style={styles.lineLabel}>Interest Share:</Text>
        <Text style={styles.lineValue}>{item.interestShare.toFixed(2)}</Text>
      </View>
      <View style={styles.lineRow}>
        <Text style={styles.lineLabel}>Gross Payout:</Text>
        <Text style={styles.lineValue}>{item.grossPayout.toFixed(2)}</Text>
      </View>
      <View style={styles.lineRow}>
        <Text style={styles.lineLabel}>Deductions:</Text>
        <Text style={[styles.lineValue, { color: '#dc2626' }]}>
          {(item.outstandingLoanPrincipal + item.unpaidInterest + item.unpaidDues).toFixed(2)}
        </Text>
      </View>
      <View style={[styles.lineRow, { borderTopWidth: 1, borderTopColor: '#e5e7eb', paddingTop: 4 }]}>
        <Text style={[styles.lineLabel, { fontWeight: '700' }]}>Net Payout:</Text>
        <Text
          style={[
            styles.lineValue,
            { fontWeight: '700', color: item.netPayout < 0 ? '#dc2626' : '#16a34a' },
          ]}
        >
          {item.netPayout.toFixed(2)}
        </Text>
      </View>
    </View>
  );

  return (
    <ScrollView style={styles.container}>
      <Text style={styles.heading}>Fund Dissolution</Text>

      {/* Initiate */}
      {!detail && !settlementQuery.isLoading && canManageFund && (
        <View style={styles.card}>
          <Text style={styles.cardTitle}>Initiate Dissolution</Text>
          <Text style={styles.description}>
            This will block new members, loans, and contributions. Active loans continue
            repayments.
          </Text>
          <TouchableOpacity
            style={[styles.btn, styles.btnRed]}
            onPress={() =>
              Alert.alert('Confirm', 'Are you sure you want to initiate dissolution?', [
                { text: 'Cancel', style: 'cancel' },
                { text: 'Initiate', style: 'destructive', onPress: () => initiate.mutate() },
              ])
            }
            disabled={initiate.isPending}
          >
            {initiate.isPending ? (
              <ActivityIndicator color="#fff" />
            ) : (
              <Text style={styles.btnText}>Initiate Dissolution</Text>
            )}
          </TouchableOpacity>
        </View>
      )}

      {settlementQuery.isLoading && <ActivityIndicator size="large" style={{ marginTop: 20 }} />}

      {detail && (
        <>
          {/* Overview */}
          <View style={styles.card}>
            <View style={styles.rowBetween}>
              <Text style={styles.cardTitle}>Settlement Overview</Text>
              <View
                style={[
                  styles.badge,
                  isConfirmed ? styles.badgeGreen : styles.badgeYellow,
                ]}
              >
                <Text style={styles.badgeText}>{detail.settlement.status}</Text>
              </View>
            </View>
            <View style={styles.statsRow}>
              <View style={styles.stat}>
                <Text style={styles.statValue}>
                  {detail.settlement.totalInterestPool.toFixed(2)}
                </Text>
                <Text style={styles.statLabel}>Interest Pool</Text>
              </View>
              <View style={styles.stat}>
                <Text style={styles.statValue}>
                  {detail.settlement.totalContributionsCollected.toFixed(2)}
                </Text>
                <Text style={styles.statLabel}>Contributions</Text>
              </View>
              <View style={styles.stat}>
                <Text style={styles.statValue}>{detail.lineItems.length}</Text>
                <Text style={styles.statLabel}>Members</Text>
              </View>
            </View>

            {!isConfirmed && canManageFund && (
              <View style={styles.actions}>
                <TouchableOpacity
                  style={[styles.btn, styles.btnBlue, { flex: 1, marginRight: 8 }]}
                  onPress={() => recalculate.mutate()}
                  disabled={recalculate.isPending}
                >
                  {recalculate.isPending ? (
                    <ActivityIndicator color="#fff" />
                  ) : (
                    <Text style={styles.btnText}>Recalculate</Text>
                  )}
                </TouchableOpacity>
                <TouchableOpacity
                  style={[
                    styles.btn,
                    styles.btnGreen,
                    { flex: 1, marginLeft: 8 },
                    !detail.canConfirm && { opacity: 0.5 },
                  ]}
                  onPress={() =>
                    Alert.alert('Confirm', 'Are you sure you want to confirm dissolution?', [
                      { text: 'Cancel', style: 'cancel' },
                      { text: 'Confirm', onPress: () => confirm.mutate() },
                    ])
                  }
                  disabled={confirm.isPending || !detail.canConfirm}
                >
                  {confirm.isPending ? (
                    <ActivityIndicator color="#fff" />
                  ) : (
                    <Text style={styles.btnText}>Confirm</Text>
                  )}
                </TouchableOpacity>
              </View>
            )}
          </View>

          {/* Blockers */}
          {detail.blockers.length > 0 && (
            <View style={[styles.card, { backgroundColor: '#fef2f2', borderColor: '#fecaca', borderWidth: 1 }]}>
              <Text style={[styles.cardTitle, { color: '#991b1b' }]}>
                Blockers ({detail.blockers.length})
              </Text>
              {detail.blockers.map((b) => (
                <View key={b.userId} style={styles.blockerRow}>
                  <Text style={{ color: '#991b1b' }}>
                    {b.userName ?? b.userId.slice(0, 8)}
                  </Text>
                  <Text style={{ color: '#dc2626', fontWeight: '600' }}>
                    Net: {b.netPayout.toFixed(2)}
                  </Text>
                </View>
              ))}
            </View>
          )}

          {/* Line items */}
          <View style={styles.card}>
            <Text style={styles.cardTitle}>Per-Member Settlement</Text>
            <FlatList
              data={detail.lineItems}
              keyExtractor={(item) => item.userId}
              renderItem={renderLineItem}
              scrollEnabled={false}
            />
          </View>
        </>
      )}

      <View style={{ height: 40 }} />
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: '#f9fafb', padding: 16 },
  heading: { fontSize: 22, fontWeight: '700', marginBottom: 16 },
  card: {
    backgroundColor: '#fff',
    borderRadius: 12,
    padding: 16,
    marginBottom: 16,
    shadowColor: '#000',
    shadowOpacity: 0.05,
    shadowRadius: 4,
    elevation: 2,
  },
  cardTitle: { fontSize: 16, fontWeight: '600', marginBottom: 8 },
  description: { fontSize: 14, color: '#6b7280', marginBottom: 12 },
  rowBetween: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center' },
  statsRow: { flexDirection: 'row', justifyContent: 'space-around', marginTop: 12 },
  stat: { alignItems: 'center' },
  statValue: { fontSize: 18, fontWeight: '700' },
  statLabel: { fontSize: 12, color: '#6b7280', marginTop: 2 },
  actions: { flexDirection: 'row', marginTop: 16 },
  btn: { paddingVertical: 12, borderRadius: 8, alignItems: 'center', justifyContent: 'center' },
  btnRed: { backgroundColor: '#dc2626' },
  btnBlue: { backgroundColor: '#2563eb' },
  btnGreen: { backgroundColor: '#16a34a' },
  btnText: { color: '#fff', fontWeight: '600', fontSize: 14 },
  badge: { paddingHorizontal: 10, paddingVertical: 4, borderRadius: 12 },
  badgeGreen: { backgroundColor: '#dcfce7' },
  badgeYellow: { backgroundColor: '#fef9c3' },
  badgeText: { fontSize: 12, fontWeight: '600' },
  blockerRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    paddingVertical: 4,
    borderBottomWidth: 1,
    borderBottomColor: '#fecaca',
  },
  lineItem: {
    backgroundColor: '#f9fafb',
    borderRadius: 8,
    padding: 12,
    marginBottom: 8,
  },
  memberId: { fontSize: 14, fontWeight: '600', marginBottom: 6 },
  lineRow: { flexDirection: 'row', justifyContent: 'space-between', paddingVertical: 2 },
  lineLabel: { fontSize: 13, color: '#6b7280' },
  lineValue: { fontSize: 13, fontWeight: '500' },
});
