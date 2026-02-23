import React, { useState } from 'react';
import {
  View,
  Text,
  FlatList,
  TouchableOpacity,
  StyleSheet,
  ActivityIndicator,
  RefreshControl,
} from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { useQuery } from '@tanstack/react-query';
import apiClient from '../../services/apiClient';

interface ContributionDue {
  id: string;
  monthYear: number;
  amountDue: number;
  amountPaid: number;
  remainingBalance: number;
  status: string;
  dueDate: string;
}

const statusColors: Record<string, { bg: string; text: string }> = {
  Pending: { bg: '#FEF3C7', text: '#92400E' },
  Paid: { bg: '#D1FAE5', text: '#065F46' },
  Partial: { bg: '#DBEAFE', text: '#1E40AF' },
  Late: { bg: '#FFEDD5', text: '#9A3412' },
  Missed: { bg: '#FEE2E2', text: '#991B1B' },
};

export default function MyDuesScreen({
  route,
}: {
  route: { params: { fundId: string } };
}) {
  const { fundId } = route.params;
  const [page, setPage] = useState(1);

  const { data, isLoading, refetch, isRefetching } = useQuery({
    queryKey: ['dues', fundId, page],
    queryFn: async () => {
      const res = await apiClient.get(
        `/api/funds/${fundId}/contributions/dues?page=${page}&pageSize=20`,
      );
      return res.data as { items: ContributionDue[]; totalCount: number };
    },
  });

  const formatCurrency = (amount: number) =>
    `₹${amount.toLocaleString('en-IN', { minimumFractionDigits: 2 })}`;

  const renderDue = ({ item }: { item: ContributionDue }) => {
    const colors = statusColors[item.status] ?? statusColors.Pending;
    return (
      <View style={styles.card}>
        <View style={styles.cardHeader}>
          <Text style={styles.monthText}>
            {String(item.monthYear).slice(0, 4)}/{String(item.monthYear).slice(4)}
          </Text>
          <View style={[styles.badge, { backgroundColor: colors.bg }]}>
            <Text style={[styles.badgeText, { color: colors.text }]}>{item.status}</Text>
          </View>
        </View>
        <View style={styles.row}>
          <View style={styles.col}>
            <Text style={styles.label}>Due</Text>
            <Text style={styles.amount}>{formatCurrency(item.amountDue)}</Text>
          </View>
          <View style={styles.col}>
            <Text style={styles.label}>Paid</Text>
            <Text style={[styles.amount, { color: '#059669' }]}>{formatCurrency(item.amountPaid)}</Text>
          </View>
          <View style={styles.col}>
            <Text style={styles.label}>Balance</Text>
            <Text style={[styles.amount, { color: '#DC2626' }]}>
              {formatCurrency(item.remainingBalance)}
            </Text>
          </View>
        </View>
      </View>
    );
  };

  if (isLoading) {
    return (
      <View style={styles.center}>
        <ActivityIndicator size="large" color="#1E40AF" />
      </View>
    );
  }

  return (
    <SafeAreaView style={styles.container}>
      <Text style={styles.title}>My Contributions</Text>
      <FlatList
        data={data?.items ?? []}
        keyExtractor={(item) => item.id}
        renderItem={renderDue}
        contentContainerStyle={styles.list}
        refreshControl={<RefreshControl refreshing={isRefetching} onRefresh={refetch} />}
        ListEmptyComponent={
          <Text style={styles.empty}>No contribution dues yet.</Text>
        }
      />
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: '#F9FAFB' },
  center: { flex: 1, justifyContent: 'center', alignItems: 'center' },
  title: { fontSize: 24, fontWeight: 'bold', color: '#111827', padding: 16 },
  list: { padding: 16, gap: 12 },
  card: {
    backgroundColor: '#fff',
    borderRadius: 12,
    padding: 16,
    borderWidth: 1,
    borderColor: '#E5E7EB',
  },
  cardHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: 12,
  },
  monthText: { fontSize: 16, fontWeight: '600', color: '#111827' },
  badge: { paddingHorizontal: 8, paddingVertical: 2, borderRadius: 12 },
  badgeText: { fontSize: 12, fontWeight: '600' },
  row: { flexDirection: 'row', justifyContent: 'space-between' },
  col: { alignItems: 'center' },
  label: { fontSize: 12, color: '#6B7280', marginBottom: 2 },
  amount: { fontSize: 14, fontWeight: '600', color: '#111827' },
  empty: { textAlign: 'center', color: '#6B7280', marginTop: 40, fontSize: 16 },
});
