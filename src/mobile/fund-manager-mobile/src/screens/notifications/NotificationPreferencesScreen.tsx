import React from 'react';
import {
  View,
  Text,
  Switch,
  StyleSheet,
  ActivityIndicator,
  Alert,
} from 'react-native';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import apiClient from '../../services/apiClient';

interface NotificationPreference {
  channel: string;
  enabled: boolean;
}

const CHANNEL_LABELS: Record<string, string> = {
  push: 'Push Notifications',
  email: 'Email',
  sms: 'SMS',
  in_app: 'In-App',
};

export default function NotificationPreferencesScreen() {
  const queryClient = useQueryClient();

  const { data: preferences, isLoading } = useQuery({
    queryKey: ['notifications', 'preferences'],
    queryFn: async () => {
      const { data } = await apiClient.get<NotificationPreference[]>(
        '/api/notifications/preferences'
      );
      return data;
    },
  });

  const mutation = useMutation({
    mutationFn: async (updated: NotificationPreference[]) => {
      const { data } = await apiClient.put<NotificationPreference[]>(
        '/api/notifications/preferences',
        updated
      );
      return data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['notifications', 'preferences'] });
    },
    onError: () => {
      Alert.alert('Error', 'Failed to update preferences. Please try again.');
    },
  });

  const handleToggle = (channel: string, currentValue: boolean) => {
    if (!preferences) return;

    const updated = preferences.map((p) =>
      p.channel === channel ? { ...p, enabled: !currentValue } : p
    );
    mutation.mutate(updated);
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
      <Text style={styles.header}>Notification Preferences</Text>
      <Text style={styles.subtitle}>
        Choose how you want to receive notifications
      </Text>

      <View style={styles.list}>
        {(preferences ?? []).map((pref) => (
          <View key={pref.channel} style={styles.row}>
            <View style={styles.labelContainer}>
              <Text style={styles.channelLabel}>
                {CHANNEL_LABELS[pref.channel] ?? pref.channel}
              </Text>
            </View>
            <Switch
              value={pref.enabled}
              onValueChange={() => handleToggle(pref.channel, pref.enabled)}
              trackColor={{ false: '#D1D5DB', true: '#93C5FD' }}
              thumbColor={pref.enabled ? '#1E40AF' : '#9CA3AF'}
              disabled={pref.channel === 'in_app'} // in_app is always on
            />
          </View>
        ))}
      </View>

      {mutation.isPending && (
        <View style={styles.saving}>
          <ActivityIndicator size="small" color="#1E40AF" />
          <Text style={styles.savingText}>Saving...</Text>
        </View>
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
    paddingHorizontal: 16,
    paddingTop: 16,
  },
  subtitle: {
    fontSize: 13,
    color: '#6B7280',
    paddingHorizontal: 16,
    paddingBottom: 16,
  },
  list: {
    backgroundColor: '#FFFFFF',
    borderTopWidth: 1,
    borderBottomWidth: 1,
    borderColor: '#E5E7EB',
  },
  row: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    paddingHorizontal: 16,
    paddingVertical: 14,
    borderBottomWidth: 1,
    borderBottomColor: '#F3F4F6',
  },
  labelContainer: { flex: 1 },
  channelLabel: {
    fontSize: 15,
    fontWeight: '500',
    color: '#111827',
  },
  saving: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    marginTop: 12,
  },
  savingText: {
    marginLeft: 8,
    fontSize: 13,
    color: '#6B7280',
  },
});
