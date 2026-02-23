import React from 'react';
import {
  View,
  Text,
  FlatList,
  TouchableOpacity,
  ActivityIndicator,
  StyleSheet,
} from 'react-native';
import { useQuery } from '@tanstack/react-query';
import apiClient from '../../services/apiClient';

interface Loan {
  id: string;
  principalAmount: number;
  outstandingPrincipal: number;
  status: string;
  requestedStartMonth: number;
  purpose?: string;
  createdAt: string;
}

interface Props {
  route: { params: { fundId: string } };
  navigation: any;
}

const statusColors: Record<string, { bg: string; text: string }> = {
  PendingApproval: { bg: '#fef3c7', text: '#92400e' },
  Approved: { bg: '#dbeafe', text: '#1e40af' },
  Active: { bg: '#d1fae5', text: '#065f46' },
  Closed: { bg: '#f3f4f6', text: '#374151' },
  Rejected: { bg: '#fee2e2', text: '#991b1b' },
};

export default function MyLoansScreen({ route, navigation }: Props) {
  const { fundId } = route.params;

  const { data, isLoading, refetch } = useQuery({
    queryKey: ['loans', fundId],
    queryFn: async () => {
      const { data } = await apiClient.get(`/api/funds/${fundId}/loans`);
      return data as { items: Loan[]; totalCount: number };
    },
  });

  const renderLoan = ({ item }: { item: Loan }) => {
    const colors = statusColors[item.status] || statusColors.Active;
    return (
      <TouchableOpacity
        style={styles.card}
        onPress={() =>
          navigation.navigate('LoanDetail', { fundId, loanId: item.id })
        }
      >
        <View style={styles.cardHeader}>
          <Text style={styles.amount}>₹{item.principalAmount.toLocaleString()}</Text>
          <View style={[styles.badge, { backgroundColor: colors.bg }]}>
            <Text style={[styles.badgeText, { color: colors.text }]}>{item.status}</Text>
          </View>
        </View>

        <View style={styles.row}>
          <Text style={styles.label}>Outstanding:</Text>
          <Text style={styles.value}>₹{item.outstandingPrincipal.toLocaleString()}</Text>
        </View>

        <View style={styles.row}>
          <Text style={styles.label}>Start Month:</Text>
          <Text style={styles.value}>{item.requestedStartMonth}</Text>
        </View>

        {item.purpose && (
          <Text style={styles.purpose} numberOfLines={1}>
            {item.purpose}
          </Text>
        )}

        <Text style={styles.date}>
          Requested: {new Date(item.createdAt).toLocaleDateString()}
        </Text>
      </TouchableOpacity>
    );
  };

  return (
    <View style={styles.container}>
      <View style={styles.header}>
        <Text style={styles.title}>My Loans</Text>
        <TouchableOpacity
          style={styles.newButton}
          onPress={() => navigation.navigate('RequestLoan', { fundId })}
        >
          <Text style={styles.newButtonText}>+ Request Loan</Text>
        </TouchableOpacity>
      </View>

      {isLoading ? (
        <ActivityIndicator size="large" color="#2563eb" style={styles.loader} />
      ) : (
        <FlatList
          data={data?.items ?? []}
          keyExtractor={(item) => item.id}
          renderItem={renderLoan}
          contentContainerStyle={styles.list}
          refreshing={isLoading}
          onRefresh={refetch}
          ListEmptyComponent={
            <Text style={styles.empty}>No loans found. Request your first loan!</Text>
          }
        />
      )}
    </View>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: '#f9fafb' },
  header: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    padding: 16,
    paddingBottom: 8,
  },
  title: { fontSize: 22, fontWeight: 'bold', color: '#111827' },
  newButton: {
    backgroundColor: '#2563eb',
    borderRadius: 8,
    paddingHorizontal: 12,
    paddingVertical: 8,
  },
  newButtonText: { color: '#fff', fontSize: 14, fontWeight: '600' },
  list: { paddingHorizontal: 16, paddingBottom: 16 },
  card: {
    backgroundColor: '#fff',
    borderRadius: 12,
    padding: 16,
    marginBottom: 12,
    borderWidth: 1,
    borderColor: '#e5e7eb',
  },
  cardHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: 8,
  },
  amount: { fontSize: 20, fontWeight: 'bold', color: '#111827' },
  badge: { borderRadius: 12, paddingHorizontal: 8, paddingVertical: 3 },
  badgeText: { fontSize: 12, fontWeight: '600' },
  row: { flexDirection: 'row', justifyContent: 'space-between', marginBottom: 4 },
  label: { fontSize: 14, color: '#6b7280' },
  value: { fontSize: 14, fontWeight: '500', color: '#111827' },
  purpose: { fontSize: 13, color: '#6b7280', fontStyle: 'italic', marginTop: 4 },
  date: { fontSize: 12, color: '#9ca3af', marginTop: 8 },
  loader: { marginTop: 40 },
  empty: { textAlign: 'center', color: '#9ca3af', marginTop: 40, fontSize: 16 },
});
