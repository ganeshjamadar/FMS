import React, { useState } from 'react';
import {
  View,
  Text,
  TextInput,
  TouchableOpacity,
  Alert,
  ScrollView,
  StyleSheet,
} from 'react-native';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import apiClient from '../../services/apiClient';
import { usePermissions } from '../../hooks/usePermissions';

interface Props {
  route: {
    params: {
      fundId: string;
    };
  };
  navigation: any;
}

export default function RequestLoanScreen({ route, navigation }: Props) {
  const { fundId } = route.params;
  const queryClient = useQueryClient();
  const { canWrite } = usePermissions(fundId);

  const [principalAmount, setPrincipalAmount] = useState('');
  const [startMonth, setStartMonth] = useState('');
  const [purpose, setPurpose] = useState('');

  const mutation = useMutation({
    mutationFn: async () => {
      const { data } = await apiClient.post(`/api/funds/${fundId}/loans`, {
        principalAmount: parseFloat(principalAmount),
        requestedStartMonth: parseInt(startMonth, 10),
        purpose: purpose || undefined,
      });
      return data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['loans'] });
      Alert.alert('Success', 'Loan request submitted successfully.');
      navigation.goBack();
    },
    onError: (error: any) => {
      const msg = error?.response?.data?.detail || 'Failed to submit loan request.';
      Alert.alert('Error', msg);
    },
  });

  const handleSubmit = () => {
    if (!principalAmount || parseFloat(principalAmount) <= 0) {
      Alert.alert('Validation', 'Please enter a valid principal amount.');
      return;
    }
    if (!startMonth || !/^\d{6}$/.test(startMonth)) {
      Alert.alert('Validation', 'Start month must be in YYYYMM format (e.g. 202601).');
      return;
    }
    mutation.mutate();
  };

  return (
    <ScrollView style={styles.container}>
      <Text style={styles.title}>Request a Loan</Text>

      <Text style={styles.label}>Principal Amount (₹)</Text>
      <TextInput
        style={styles.input}
        value={principalAmount}
        onChangeText={setPrincipalAmount}
        keyboardType="decimal-pad"
        placeholder="e.g. 50000"
      />

      <Text style={styles.label}>Start Month (YYYYMM)</Text>
      <TextInput
        style={styles.input}
        value={startMonth}
        onChangeText={setStartMonth}
        keyboardType="number-pad"
        placeholder="e.g. 202601"
        maxLength={6}
      />

      <Text style={styles.label}>Purpose (optional)</Text>
      <TextInput
        style={[styles.input, styles.textArea]}
        value={purpose}
        onChangeText={setPurpose}
        placeholder="Describe loan purpose..."
        multiline
        numberOfLines={3}
      />

      <TouchableOpacity
        style={[styles.button, (mutation.isPending || !canWrite) && styles.buttonDisabled]}
        onPress={handleSubmit}
        disabled={mutation.isPending || !canWrite}
      >
        <Text style={styles.buttonText}>
          {mutation.isPending ? 'Submitting...' : 'Submit Loan Request'}
        </Text>
      </TouchableOpacity>
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: '#f9fafb', padding: 16 },
  title: { fontSize: 22, fontWeight: 'bold', color: '#111827', marginBottom: 24 },
  label: { fontSize: 14, fontWeight: '600', color: '#374151', marginBottom: 6, marginTop: 12 },
  input: {
    backgroundColor: '#fff',
    borderWidth: 1,
    borderColor: '#d1d5db',
    borderRadius: 8,
    padding: 12,
    fontSize: 16,
  },
  textArea: { height: 80, textAlignVertical: 'top' },
  button: {
    backgroundColor: '#2563eb',
    borderRadius: 8,
    padding: 14,
    alignItems: 'center',
    marginTop: 24,
  },
  buttonDisabled: { opacity: 0.6 },
  buttonText: { color: '#fff', fontSize: 16, fontWeight: '600' },
});
