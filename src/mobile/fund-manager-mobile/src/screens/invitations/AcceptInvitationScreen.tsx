import React, { useState } from 'react';
import {
  View,
  Text,
  TextInput,
  TouchableOpacity,
  StyleSheet,
  Alert,
  ActivityIndicator,
} from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import apiClient from '../../services/apiClient';

export default function AcceptInvitationScreen({
  route,
}: {
  route: { params: { invitationId: string; fundName?: string; minimumContribution?: number } };
}) {
  const { invitationId, fundName, minimumContribution } = route.params;
  const queryClient = useQueryClient();
  const [amount, setAmount] = useState(minimumContribution?.toString() ?? '');

  const acceptMutation = useMutation({
    mutationFn: async (monthlyContributionAmount: number) => {
      const res = await apiClient.post(
        `/api/invitations/${invitationId}/accept`,
        { monthlyContributionAmount },
      );
      return res.data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['funds'] });
      Alert.alert('Success', 'You are now a member of the fund!');
    },
    onError: (err: Error) => {
      Alert.alert('Error', err.message ?? 'Failed to accept invitation');
    },
  });

  const handleAccept = () => {
    const value = parseFloat(amount);
    if (isNaN(value) || value <= 0) {
      Alert.alert('Invalid', 'Please enter a valid contribution amount.');
      return;
    }
    if (minimumContribution && value < minimumContribution) {
      Alert.alert('Too Low', `Minimum contribution is ₹${minimumContribution}`);
      return;
    }
    acceptMutation.mutate(value);
  };

  return (
    <SafeAreaView style={styles.container}>
      <View style={styles.content}>
        <Text style={styles.title}>Accept Invitation</Text>
        {fundName && (
          <Text style={styles.fundName}>{fundName}</Text>
        )}

        <Text style={styles.label}>Monthly Contribution Amount (₹)</Text>
        <TextInput
          style={styles.input}
          value={amount}
          onChangeText={setAmount}
          keyboardType="decimal-pad"
          placeholder={`Min ₹${minimumContribution ?? 0}`}
        />
        {minimumContribution && (
          <Text style={styles.hint}>Minimum: ₹{minimumContribution.toLocaleString()}</Text>
        )}

        <TouchableOpacity
          style={[styles.button, acceptMutation.isPending && styles.buttonDisabled]}
          onPress={handleAccept}
          disabled={acceptMutation.isPending}
        >
          {acceptMutation.isPending ? (
            <ActivityIndicator color="#fff" />
          ) : (
            <Text style={styles.buttonText}>Accept & Join</Text>
          )}
        </TouchableOpacity>
      </View>
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: '#F9FAFB' },
  content: { padding: 24 },
  title: { fontSize: 24, fontWeight: 'bold', color: '#111827', marginBottom: 8 },
  fundName: { fontSize: 16, color: '#6B7280', marginBottom: 24 },
  label: { fontSize: 14, fontWeight: '500', color: '#374151', marginBottom: 8 },
  input: {
    backgroundColor: '#fff',
    borderWidth: 1,
    borderColor: '#D1D5DB',
    borderRadius: 10,
    paddingHorizontal: 16,
    paddingVertical: 12,
    fontSize: 16,
    color: '#111827',
  },
  hint: { fontSize: 12, color: '#9CA3AF', marginTop: 4 },
  button: {
    backgroundColor: '#1E40AF',
    borderRadius: 10,
    paddingVertical: 14,
    alignItems: 'center',
    marginTop: 24,
  },
  buttonDisabled: { opacity: 0.6 },
  buttonText: { color: '#fff', fontSize: 16, fontWeight: '600' },
});
