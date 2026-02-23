import { Platform } from 'react-native';
import apiClient from './apiClient';

interface QueuedRequest {
  id: string;
  url: string;
  method: 'POST' | 'PUT' | 'PATCH' | 'DELETE';
  data?: unknown;
  idempotencyKey: string;
  createdAt: string;
}

const QUEUE_KEY = '@fund_manager_offline_queue';

/**
 * Offline sync manager.
 * Queues failed mutation requests and replays them when connectivity is restored.
 * Per research-frontend.md Section 5.
 */
export class OfflineSyncManager {
  private queue: QueuedRequest[] = [];
  private isSyncing = false;

  async enqueue(request: Omit<QueuedRequest, 'id' | 'createdAt'>): Promise<void> {
    const item: QueuedRequest = {
      ...request,
      id: `${Date.now()}-${Math.random().toString(36).substring(2, 9)}`,
      createdAt: new Date().toISOString(),
    };
    this.queue.push(item);
    await this.persistQueue();
  }

  async sync(): Promise<{ succeeded: number; failed: number }> {
    if (this.isSyncing || this.queue.length === 0) {
      return { succeeded: 0, failed: 0 };
    }

    this.isSyncing = true;
    let succeeded = 0;
    let failed = 0;

    const pending = [...this.queue];

    for (const item of pending) {
      try {
        await apiClient.request({
          url: item.url,
          method: item.method,
          data: item.data,
          headers: {
            'Idempotency-Key': item.idempotencyKey,
          },
        });
        this.queue = this.queue.filter((q) => q.id !== item.id);
        succeeded++;
      } catch {
        failed++;
      }
    }

    await this.persistQueue();
    this.isSyncing = false;

    return { succeeded, failed };
  }

  get pendingCount(): number {
    return this.queue.length;
  }

  private async persistQueue(): Promise<void> {
    // AsyncStorage will be used in the actual implementation
    // For now this is a placeholder — storage adapter injected later
    if (Platform.OS !== 'web') {
      // Will use expo-secure-store or AsyncStorage
    }
  }

  async loadQueue(): Promise<void> {
    // Will load from persistent storage on app startup
  }
}

export const offlineSyncManager = new OfflineSyncManager();
