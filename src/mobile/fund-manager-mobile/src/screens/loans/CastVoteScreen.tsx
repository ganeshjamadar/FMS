import React, { useState } from 'react';
import {
  View,
  Text,
  StyleSheet,
  ScrollView,
  TouchableOpacity,
  ActivityIndicator,
  Alert,
  TextInput,
  FlatList,
} from 'react-native';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import type { NativeStackScreenProps } from '@react-navigation/native-stack';
import type { RootStackParamList } from '../../navigation/RootNavigator';
import apiClient from '../../services/apiClient';
import { usePermissions } from '../../hooks/usePermissions';

interface VoteSummary {
  voterId: string;
  voterName?: string;
  decision: 'Approve' | 'Reject';
  castAt: string;
}

interface VotingSession {
  id: string;
  loanId: string;
  votingWindowStart: string;
  votingWindowEnd: string;
  thresholdType: string;
  thresholdValue: number;
  result: string;
  overrideUsed: boolean;
  finalisedBy?: string;
  finalisedDate?: string;
}

interface VotingSessionDetail extends VotingSession {
  approveCount: number;
  rejectCount: number;
  totalEligible: number;
  votes: VoteSummary[];
}

type Props = NativeStackScreenProps<RootStackParamList, 'Voting'>;

export default function CastVoteScreen({ route }: Props) {
  const { fundId, loanId } = route.params;
  const qc = useQueryClient();
  const { canManageFund, canWrite } = usePermissions(fundId);
  const [sessionId, setSessionId] = useState('');
  const [sessionInput, setSessionInput] = useState('');
  const [windowHours, setWindowHours] = useState('48');

  const sessionQuery = useQuery({
    queryKey: ['voting', loanId, sessionId],
    queryFn: async () => {
      const { data } = await apiClient.get<VotingSessionDetail>(
        `/api/funds/${fundId}/loans/${loanId}/voting/${sessionId}`
      );
      return data;
    },
    enabled: !!sessionId,
  });

  const startVoting = useMutation({
    mutationFn: async () => {
      const { data } = await apiClient.post<VotingSession>(
        `/api/funds/${fundId}/loans/${loanId}/voting/start`,
        { votingWindowHours: Number(windowHours) || 48 }
      );
      return data;
    },
    onSuccess: (data) => {
      setSessionId(data.id);
      qc.invalidateQueries({ queryKey: ['voting'] });
    },
    onError: () => Alert.alert('Error', 'Failed to start voting session.'),
  });

  const castVote = useMutation({
    mutationFn: async (decision: 'Approve' | 'Reject') => {
      await apiClient.post(
        `/api/funds/${fundId}/loans/${loanId}/voting/${sessionId}/vote`,
        { decision }
      );
    },
    onSuccess: () => {
      Alert.alert('Success', 'Vote recorded.');
      qc.invalidateQueries({ queryKey: ['voting', loanId, sessionId] });
    },
    onError: () => Alert.alert('Error', 'Failed to cast vote. You may have already voted.'),
  });

  const finalise = useMutation({
    mutationFn: async (decision: 'Approve' | 'Reject') => {
      await apiClient.post(
        `/api/funds/${fundId}/loans/${loanId}/voting/${sessionId}/finalise`,
        { decision }
      );
    },
    onSuccess: () => {
      Alert.alert('Success', 'Voting finalised.');
      qc.invalidateQueries({ queryKey: ['voting', loanId, sessionId] });
    },
    onError: () => Alert.alert('Error', 'Failed to finalise voting.'),
  });

  const session = sessionQuery.data;

  const renderVote = ({ item }: { item: VoteSummary }) => (
    <View style={styles.voteRow}>
      <Text style={styles.voterName}>{item.voterName ?? item.voterId.slice(0, 8)}</Text>
      <Text style={[styles.decision, item.decision === 'Approve' ? styles.approve : styles.reject]}>
        {item.decision}
      </Text>
      <Text style={styles.castAt}>{new Date(item.castAt).toLocaleString()}</Text>
    </View>
  );

  return (
    <ScrollView style={styles.container}>
      <Text style={styles.heading}>Loan Voting</Text>

      {/* Start or load session */}
      {!sessionId && (
        <View style={styles.card}>
          <Text style={styles.cardTitle}>Start New Session</Text>
          <View style={styles.row}>
            <Text style={styles.label}>Window (hours):</Text>
            <TextInput
              style={styles.input}
              value={windowHours}
              onChangeText={setWindowHours}
              keyboardType="numeric"
            />
          </View>
          <TouchableOpacity
            style={[styles.btn, styles.btnBlue]}
            onPress={() => startVoting.mutate()}
            disabled={startVoting.isPending || !canManageFund}
          >
            {startVoting.isPending ? (
              <ActivityIndicator color="#fff" />
            ) : (
              <Text style={styles.btnText}>Start Voting</Text>
            )}
          </TouchableOpacity>

          <Text style={[styles.cardTitle, { marginTop: 20 }]}>Load Existing Session</Text>
          <TextInput
            style={styles.input}
            value={sessionInput}
            onChangeText={setSessionInput}
            placeholder="Enter session ID"
          />
          <TouchableOpacity
            style={[styles.btn, styles.btnGray]}
            onPress={() => {
              const v = sessionInput.trim();
              if (v) setSessionId(v);
            }}
          >
            <Text style={styles.btnText}>Load</Text>
          </TouchableOpacity>
        </View>
      )}

      {sessionId && sessionQuery.isLoading && (
        <ActivityIndicator size="large" style={{ marginTop: 20 }} />
      )}

      {session && (
        <>
          {/* Status */}
          <View style={styles.card}>
            <View style={styles.rowBetween}>
              <Text style={styles.cardTitle}>Session</Text>
              <View
                style={[
                  styles.badge,
                  session.result === 'Approved'
                    ? styles.badgeGreen
                    : session.result === 'Rejected'
                      ? styles.badgeRed
                      : styles.badgeYellow,
                ]}
              >
                <Text style={styles.badgeText}>{session.result}</Text>
              </View>
            </View>
            <Text style={styles.meta}>
              Window: {new Date(session.votingWindowStart).toLocaleString()} –{' '}
              {new Date(session.votingWindowEnd).toLocaleString()}
            </Text>
            <Text style={styles.meta}>
              Threshold: {session.thresholdType} ≥ {session.thresholdValue}
            </Text>
            {session.overrideUsed && (
              <Text style={styles.override}>⚠ Admin override used</Text>
            )}
          </View>

          {/* Tally */}
          <View style={styles.card}>
            <Text style={styles.cardTitle}>Vote Tally</Text>
            <View style={styles.tallyRow}>
              <View style={styles.tallyItem}>
                <Text style={[styles.tallyNum, { color: '#16a34a' }]}>
                  {session.approveCount}
                </Text>
                <Text style={styles.tallyLabel}>Approve</Text>
              </View>
              <View style={styles.tallyItem}>
                <Text style={[styles.tallyNum, { color: '#dc2626' }]}>
                  {session.rejectCount}
                </Text>
                <Text style={styles.tallyLabel}>Reject</Text>
              </View>
              <View style={styles.tallyItem}>
                <Text style={[styles.tallyNum, { color: '#9ca3af' }]}>
                  {session.totalEligible - session.approveCount - session.rejectCount}
                </Text>
                <Text style={styles.tallyLabel}>Abstained</Text>
              </View>
            </View>
          </View>

          {/* Cast vote */}
          {session.result === 'Pending' &&
            new Date(session.votingWindowEnd) > new Date() &&
            canWrite && (
              <View style={styles.card}>
                <Text style={styles.cardTitle}>Cast Your Vote</Text>
                <View style={styles.voteButtons}>
                  <TouchableOpacity
                    style={[styles.btn, styles.btnGreen, { flex: 1, marginRight: 8 }]}
                    onPress={() => castVote.mutate('Approve')}
                    disabled={castVote.isPending}
                  >
                    {castVote.isPending ? (
                      <ActivityIndicator color="#fff" />
                    ) : (
                      <Text style={styles.btnText}>✓ Approve</Text>
                    )}
                  </TouchableOpacity>
                  <TouchableOpacity
                    style={[styles.btn, styles.btnRed, { flex: 1, marginLeft: 8 }]}
                    onPress={() => castVote.mutate('Reject')}
                    disabled={castVote.isPending}
                  >
                    {castVote.isPending ? (
                      <ActivityIndicator color="#fff" />
                    ) : (
                      <Text style={styles.btnText}>✗ Reject</Text>
                    )}
                  </TouchableOpacity>
                </View>
              </View>
            )}

          {/* Finalise (admin) */}
          {session.result === 'Pending' && canManageFund && (
            <View style={styles.card}>
              <Text style={styles.cardTitle}>Finalise (Admin)</Text>
              <Text style={styles.meta}>
                Decision may override the natural tally result.
              </Text>
              <View style={styles.voteButtons}>
                <TouchableOpacity
                  style={[styles.btn, styles.btnGreen, { flex: 1, marginRight: 8 }]}
                  onPress={() => finalise.mutate('Approve')}
                  disabled={finalise.isPending}
                >
                  {finalise.isPending ? (
                    <ActivityIndicator color="#fff" />
                  ) : (
                    <Text style={styles.btnText}>Approve</Text>
                  )}
                </TouchableOpacity>
                <TouchableOpacity
                  style={[styles.btn, styles.btnRed, { flex: 1, marginLeft: 8 }]}
                  onPress={() => finalise.mutate('Reject')}
                  disabled={finalise.isPending}
                >
                  {finalise.isPending ? (
                    <ActivityIndicator color="#fff" />
                  ) : (
                    <Text style={styles.btnText}>Reject</Text>
                  )}
                </TouchableOpacity>
              </View>
            </View>
          )}

          {/* Votes list */}
          {session.votes.length > 0 && (
            <View style={styles.card}>
              <Text style={styles.cardTitle}>Individual Votes</Text>
              <FlatList
                data={session.votes}
                keyExtractor={(v) => v.voterId}
                renderItem={renderVote}
                scrollEnabled={false}
              />
            </View>
          )}
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
  row: { flexDirection: 'row', alignItems: 'center', marginBottom: 12 },
  rowBetween: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center' },
  label: { fontSize: 14, color: '#6b7280', marginRight: 8 },
  input: {
    borderWidth: 1,
    borderColor: '#d1d5db',
    borderRadius: 8,
    paddingHorizontal: 12,
    paddingVertical: 8,
    fontSize: 14,
    flex: 1,
    marginBottom: 8,
  },
  btn: { paddingVertical: 12, borderRadius: 8, alignItems: 'center', justifyContent: 'center' },
  btnBlue: { backgroundColor: '#2563eb' },
  btnGray: { backgroundColor: '#6b7280' },
  btnGreen: { backgroundColor: '#16a34a' },
  btnRed: { backgroundColor: '#dc2626' },
  btnText: { color: '#fff', fontWeight: '600', fontSize: 14 },
  meta: { fontSize: 13, color: '#6b7280', marginBottom: 4 },
  override: {
    marginTop: 8,
    backgroundColor: '#fffbeb',
    borderWidth: 1,
    borderColor: '#fbbf24',
    borderRadius: 6,
    padding: 8,
    fontSize: 13,
    color: '#92400e',
  },
  badge: { paddingHorizontal: 10, paddingVertical: 4, borderRadius: 12 },
  badgeGreen: { backgroundColor: '#dcfce7' },
  badgeRed: { backgroundColor: '#fee2e2' },
  badgeYellow: { backgroundColor: '#fef9c3' },
  badgeText: { fontSize: 12, fontWeight: '600' },
  tallyRow: { flexDirection: 'row', justifyContent: 'space-around', marginTop: 8 },
  tallyItem: { alignItems: 'center' },
  tallyNum: { fontSize: 28, fontWeight: '700' },
  tallyLabel: { fontSize: 12, color: '#6b7280', marginTop: 2 },
  voteButtons: { flexDirection: 'row', marginTop: 12 },
  voteRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    paddingVertical: 8,
    borderBottomWidth: 1,
    borderBottomColor: '#f3f4f6',
  },
  voterName: { fontSize: 14, fontWeight: '500', flex: 1 },
  decision: { fontSize: 14, fontWeight: '600', marginHorizontal: 8 },
  approve: { color: '#16a34a' },
  reject: { color: '#dc2626' },
  castAt: { fontSize: 12, color: '#9ca3af' },
});
