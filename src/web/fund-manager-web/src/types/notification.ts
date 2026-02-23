export interface NotificationFeedItem {
  id: string;
  fundId: string | null;
  fundName: string | null;
  channel: string;
  templateKey: string;
  title: string;
  body: string;
  status: string;
  scheduledAt: string;
  sentAt: string | null;
}

export interface PaginatedNotificationFeed {
  items: NotificationFeedItem[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface NotificationPreference {
  channel: string;
  enabled: boolean;
}

export interface UpdatePreferenceRequest {
  channel: string;
  enabled: boolean;
}

export interface RegisterDeviceRequest {
  deviceId: string;
  pushToken: string;
  platform: 'ios' | 'android';
}

export interface UnreadCount {
  count: number;
}
