import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { api } from '@/services/apiClient';
import type {
  PaginatedNotificationFeed,
  UnreadCount,
  NotificationPreference,
  UpdatePreferenceRequest,
} from '@/types/notification';

export const notificationKeys = {
  all: () => ['notifications'] as const,
  feed: (page: number, fundId?: string) =>
    ['notifications', 'feed', page, fundId] as const,
  unreadCount: () => ['notifications', 'unread-count'] as const,
  preferences: () => ['notifications', 'preferences'] as const,
};

// ── Feed ────────────────────────────────────────

export function useNotificationFeed(page = 1, fundId?: string) {
  return useQuery({
    queryKey: notificationKeys.feed(page, fundId),
    queryFn: () =>
      api.get<PaginatedNotificationFeed>('/notifications/feed', {
        page,
        pageSize: 20,
        ...(fundId ? { fundId } : {}),
      }),
    refetchInterval: 30_000, // Poll every 30s
  });
}

// ── Unread Count ────────────────────────────────

export function useUnreadCount() {
  return useQuery({
    queryKey: notificationKeys.unreadCount(),
    queryFn: () => api.get<UnreadCount>('/notifications/feed/unread-count'),
    refetchInterval: 15_000, // Poll every 15s
  });
}

// ── Preferences ─────────────────────────────────

export function useNotificationPreferences() {
  return useQuery({
    queryKey: notificationKeys.preferences(),
    queryFn: () => api.get<NotificationPreference[]>('/notifications/preferences'),
  });
}

export function useUpdatePreferences() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (preferences: UpdatePreferenceRequest[]) =>
      api.put<NotificationPreference[]>('/notifications/preferences', preferences),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: notificationKeys.preferences() });
    },
  });
}
