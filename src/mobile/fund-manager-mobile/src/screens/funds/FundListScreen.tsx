import React, { useState, useCallback } from 'react';
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
import { useNavigation } from '@react-navigation/native';
import { useQuery } from '@tanstack/react-query';
import apiClient from '../../services/apiClient';

// ── Types ──

interface Fund {
  id: string;
  name: string;
  description?: string;
  currency: string;
  monthlyInterestRate: number;
  minimumMonthlyContribution: number;
  state: string;
  createdAt: string;
}

interface PaginatedResponse<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}

type FundState = '' | 'Draft' | 'Active' | 'Dissolving' | 'Dissolved';

const STATE_COLORS: Record<string, { bg: string; text: string }> = {
  Draft: { bg: '#FEF3C7', text: '#92400E' },
  Active: { bg: '#D1FAE5', text: '#065F46' },
  Dissolving: { bg: '#FFEDD5', text: '#9A3412' },
  Dissolved: { bg: '#F3F4F6', text: '#4B5563' },
};

const STATE_FILTERS: { label: string; value: FundState }[] = [
  { label: 'All', value: '' },
  { label: 'Draft', value: 'Draft' },
  { label: 'Active', value: 'Active' },
  { label: 'Dissolving', value: 'Dissolving' },
  { label: 'Dissolved', value: 'Dissolved' },
];

// ── Component ──

export default function FundListScreen() {
  const [stateFilter, setStateFilter] = useState<FundState>('');
  const navigation = useNavigation<any>();

  const {
    data,
    isLoading,
    isError,
    error,
    refetch,
    isRefetching,
  } = useQuery({
    queryKey: ['funds', 'list', { state: stateFilter }],
    queryFn: async () => {
      const params: Record<string, unknown> = { page: 1, pageSize: 50 };
      if (stateFilter) params.state = stateFilter;
      const response = await apiClient.get<PaginatedResponse<Fund>>(
        '/api/funds',
        { params },
      );
      return response.data;
    },
  });

  const onRefresh = useCallback(() => {
    refetch();
  }, [refetch]);

  const renderFundItem = ({ item }: { item: Fund }) => {
    const colors = STATE_COLORS[item.state] ?? STATE_COLORS.Dissolved;
    return (
      <TouchableOpacity
        style={styles.card}
        activeOpacity={0.7}
        onPress={() => navigation.navigate('FundDetail', { fundId: item.id })}
      >
        <View style={styles.cardHeader}>
          <Text style={styles.fundName} numberOfLines={1}>
            {item.name}
          </Text>
          <View style={[styles.badge, { backgroundColor: colors.bg }]}>
            <Text style={[styles.badgeText, { color: colors.text }]}>
              {item.state}
            </Text>
          </View>
        </View>
        {item.description ? (
          <Text style={styles.description} numberOfLines={2}>
            {item.description}
          </Text>
        ) : null}
        <View style={styles.cardFooter}>
          <Text style={styles.detail}>
            {item.currency}{' '}
            {item.minimumMonthlyContribution.toLocaleString()} /mo
          </Text>
          <Text style={styles.detail}>
            {(item.monthlyInterestRate * 100).toFixed(2)}% interest
          </Text>
        </View>
      </TouchableOpacity>
    );
  };

  return (
    <SafeAreaView style={styles.container}>
      <Text style={styles.title}>Funds</Text>

      {/* State Filter Chips */}
      <View style={styles.filterRow}>
        {STATE_FILTERS.map((opt) => (
          <TouchableOpacity
            key={opt.value}
            onPress={() => setStateFilter(opt.value)}
            style={[
              styles.filterChip,
              stateFilter === opt.value && styles.filterChipActive,
            ]}
          >
            <Text
              style={[
                styles.filterChipText,
                stateFilter === opt.value && styles.filterChipTextActive,
              ]}
            >
              {opt.label}
            </Text>
          </TouchableOpacity>
        ))}
      </View>

      {/* Content */}
      {isLoading && (
        <View style={styles.center}>
          <ActivityIndicator size="large" color="#1E40AF" />
        </View>
      )}

      {isError && (
        <View style={styles.center}>
          <Text style={styles.errorText}>
            Failed to load funds: {(error as Error).message}
          </Text>
          <TouchableOpacity onPress={onRefresh} style={styles.retryButton}>
            <Text style={styles.retryText}>Retry</Text>
          </TouchableOpacity>
        </View>
      )}

      {data && (
        <FlatList
          data={data.items}
          keyExtractor={(item) => item.id}
          renderItem={renderFundItem}
          contentContainerStyle={styles.listContent}
          refreshControl={
            <RefreshControl
              refreshing={isRefetching}
              onRefresh={onRefresh}
              tintColor="#1E40AF"
            />
          }
          ListEmptyComponent={
            <View style={styles.center}>
              <Text style={styles.emptyText}>No funds found</Text>
            </View>
          }
        />
      )}
    </SafeAreaView>
  );
}

// ── Styles ──

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#F9FAFB',
  },
  title: {
    fontSize: 24,
    fontWeight: 'bold',
    color: '#111827',
    paddingHorizontal: 16,
    paddingTop: 16,
    paddingBottom: 8,
  },
  filterRow: {
    flexDirection: 'row',
    paddingHorizontal: 16,
    paddingBottom: 12,
    gap: 8,
  },
  filterChip: {
    paddingHorizontal: 14,
    paddingVertical: 6,
    borderRadius: 16,
    backgroundColor: '#F3F4F6',
  },
  filterChipActive: {
    backgroundColor: '#1E40AF',
  },
  filterChipText: {
    fontSize: 13,
    fontWeight: '500',
    color: '#374151',
  },
  filterChipTextActive: {
    color: '#FFFFFF',
  },
  listContent: {
    paddingHorizontal: 16,
    paddingBottom: 24,
  },
  card: {
    backgroundColor: '#FFFFFF',
    borderRadius: 12,
    padding: 16,
    marginBottom: 12,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 1 },
    shadowOpacity: 0.05,
    shadowRadius: 3,
    elevation: 2,
  },
  cardHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: 4,
  },
  fundName: {
    fontSize: 16,
    fontWeight: '600',
    color: '#111827',
    flex: 1,
    marginRight: 8,
  },
  badge: {
    paddingHorizontal: 8,
    paddingVertical: 2,
    borderRadius: 10,
  },
  badgeText: {
    fontSize: 11,
    fontWeight: '600',
  },
  description: {
    fontSize: 13,
    color: '#6B7280',
    marginBottom: 8,
  },
  cardFooter: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    marginTop: 8,
  },
  detail: {
    fontSize: 12,
    color: '#9CA3AF',
  },
  center: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
    padding: 32,
  },
  errorText: {
    fontSize: 14,
    color: '#DC2626',
    textAlign: 'center',
    marginBottom: 12,
  },
  retryButton: {
    paddingHorizontal: 20,
    paddingVertical: 8,
    backgroundColor: '#1E40AF',
    borderRadius: 8,
  },
  retryText: {
    color: '#FFFFFF',
    fontWeight: '600',
  },
  emptyText: {
    fontSize: 14,
    color: '#9CA3AF',
  },
});
