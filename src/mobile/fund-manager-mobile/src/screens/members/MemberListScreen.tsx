import React from 'react';
import {
  View,
  Text,
  FlatList,
  StyleSheet,
  ActivityIndicator,
  RefreshControl,
} from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { useQuery } from '@tanstack/react-query';
import apiClient from '../../services/apiClient';

interface MemberSummary {
  userId: string;
  name: string;
  role: string;
  monthlyContributionAmount?: number;
  joinDate?: string;
  isActive: boolean;
}

interface PaginatedResponse<T> {
  items: T[];
  totalCount: number;
}

const ROLE_COLORS: Record<string, { bg: string; text: string }> = {
  Admin: { bg: '#F3E8FF', text: '#6B21A8' },
  Editor: { bg: '#DBEAFE', text: '#1E40AF' },
  Guest: { bg: '#F3F4F6', text: '#4B5563' },
};

export default function MemberListScreen({ route }: { route: { params: { fundId: string } } }) {
  const fundId = route.params.fundId;

  const { data, isLoading, refetch, isRefetching } = useQuery({
    queryKey: ['funds', fundId, 'members'],
    queryFn: async () => {
      const res = await apiClient.get<PaginatedResponse<MemberSummary>>(
        `/api/funds/${fundId}/members`,
        { params: { page: 1, pageSize: 100 } },
      );
      return res.data;
    },
    enabled: !!fundId,
  });

  const renderMember = ({ item }: { item: MemberSummary }) => {
    const colors = ROLE_COLORS[item.role] ?? ROLE_COLORS.Guest;
    return (
      <View style={styles.card}>
        <View style={styles.row}>
          <Text style={styles.name}>{item.name || item.userId.slice(0, 8)}</Text>
          <View style={[styles.badge, { backgroundColor: colors.bg }]}>
            <Text style={[styles.badgeText, { color: colors.text }]}>{item.role}</Text>
          </View>
        </View>
        <View style={styles.row}>
          <Text style={styles.detail}>
            {item.monthlyContributionAmount
              ? `₹${item.monthlyContributionAmount.toLocaleString()} /mo`
              : '—'}
          </Text>
          <Text style={styles.detail}>
            {item.isActive ? 'Active' : 'Inactive'}
          </Text>
        </View>
      </View>
    );
  };

  if (isLoading) {
    return (
      <SafeAreaView style={styles.container}>
        <View style={styles.center}>
          <ActivityIndicator size="large" color="#1E40AF" />
        </View>
      </SafeAreaView>
    );
  }

  return (
    <SafeAreaView style={styles.container}>
      <Text style={styles.title}>Members</Text>
      <FlatList
        data={data?.items ?? []}
        keyExtractor={(item) => item.userId}
        renderItem={renderMember}
        contentContainerStyle={styles.list}
        refreshControl={
          <RefreshControl refreshing={isRefetching} onRefresh={() => refetch()} tintColor="#1E40AF" />
        }
        ListEmptyComponent={
          <View style={styles.center}>
            <Text style={styles.emptyText}>No members yet</Text>
          </View>
        }
      />
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: '#F9FAFB' },
  title: {
    fontSize: 22,
    fontWeight: 'bold',
    color: '#111827',
    paddingHorizontal: 16,
    paddingTop: 16,
    paddingBottom: 12,
  },
  list: { paddingHorizontal: 16, paddingBottom: 24 },
  card: {
    backgroundColor: '#fff',
    borderRadius: 10,
    padding: 14,
    marginBottom: 10,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 1 },
    shadowOpacity: 0.04,
    shadowRadius: 2,
    elevation: 1,
  },
  row: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: 4,
  },
  name: { fontSize: 15, fontWeight: '600', color: '#111827' },
  badge: { paddingHorizontal: 8, paddingVertical: 2, borderRadius: 8 },
  badgeText: { fontSize: 11, fontWeight: '600' },
  detail: { fontSize: 12, color: '#9CA3AF' },
  center: { flex: 1, justifyContent: 'center', alignItems: 'center', padding: 32 },
  emptyText: { fontSize: 14, color: '#9CA3AF' },
});
