import React from 'react';
import {
  View,
  Text,
  FlatList,
  StyleSheet,
  ActivityIndicator,
  RefreshControl,
} from 'react-native';
import { useQuery } from '@tanstack/react-query';
import apiClient from '../../services/apiClient';

interface NotificationFeedItem {
  id: string;
  fundId: string | null;
  channel: string;
  templateKey: string;
  title: string;
  body: string;
  status: string;
  scheduledAt: string;
  sentAt: string | null;
}

interface PaginatedFeed {
  items: NotificationFeedItem[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export default function NotificationFeedScreen() {
  const {
    data,
    isLoading,
    refetch,
    isRefetching,
  } = useQuery({
    queryKey: ['notifications', 'feed'],
    queryFn: async () => {
      const { data } = await apiClient.get<PaginatedFeed>('/api/notifications/feed', {
        params: { page: 1, pageSize: 50 },
      });
      return data;
    },
    refetchInterval: 30_000,
  });

  const items = data?.items ?? [];

  if (isLoading) {
    return (
      <View style={styles.center}>
        <ActivityIndicator size="large" color="#1E40AF" />
      </View>
    );
  }

  return (
    <View style={styles.container}>
      <Text style={styles.header}>Notifications</Text>

      {items.length === 0 ? (
        <View style={styles.center}>
          <Text style={styles.emptyText}>No notifications yet</Text>
        </View>
      ) : (
        <FlatList
          data={items}
          keyExtractor={(item) => item.id}
          refreshControl={
            <RefreshControl refreshing={isRefetching} onRefresh={refetch} />
          }
          renderItem={({ item }) => (
            <View
              style={[
                styles.card,
                item.status === 'Pending' && styles.unread,
              ]}
            >
              <Text style={styles.title}>{item.title}</Text>
              <Text style={styles.body} numberOfLines={3}>
                {item.body}
              </Text>
              <Text style={styles.time}>
                {new Date(item.scheduledAt).toLocaleDateString(undefined, {
                  month: 'short',
                  day: 'numeric',
                  hour: '2-digit',
                  minute: '2-digit',
                })}
              </Text>
            </View>
          )}
          ItemSeparatorComponent={() => <View style={styles.separator} />}
        />
      )}
    </View>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: '#F9FAFB' },
  center: { flex: 1, justifyContent: 'center', alignItems: 'center' },
  header: {
    fontSize: 20,
    fontWeight: '700',
    color: '#111827',
    padding: 16,
    backgroundColor: '#FFFFFF',
    borderBottomWidth: 1,
    borderBottomColor: '#E5E7EB',
  },
  card: {
    backgroundColor: '#FFFFFF',
    padding: 16,
  },
  unread: {
    backgroundColor: '#EFF6FF',
    borderLeftWidth: 3,
    borderLeftColor: '#1E40AF',
  },
  title: {
    fontSize: 15,
    fontWeight: '600',
    color: '#111827',
  },
  body: {
    fontSize: 13,
    color: '#6B7280',
    marginTop: 4,
  },
  time: {
    fontSize: 11,
    color: '#9CA3AF',
    marginTop: 6,
  },
  separator: {
    height: 1,
    backgroundColor: '#F3F4F6',
  },
  emptyText: {
    fontSize: 15,
    color: '#9CA3AF',
  },
});
