import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { api } from '@/services/apiClient';
import { useAuthStore } from '@/stores/authStore';
import logo from '@/images/logo/Web-Logo.png';

interface OtpRequestResponse {
  challengeId: string;
  expiresAt: string;
  message: string;
}

interface OtpVerifyResponse {
  token: string;
  expiresAt: string;
  userId: string;
}

type Step = 'phone' | 'otp';

export default function LoginPage() {
  const navigate = useNavigate();
  const setToken = useAuthStore((s) => s.setToken);

  const [step, setStep] = useState<Step>('phone');
  const [phone, setPhone] = useState('');
  const [challengeId, setChallengeId] = useState('');
  const [otp, setOtp] = useState('');
  const [message, setMessage] = useState('');
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

  const handleRequestOtp = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');
    setLoading(true);
    try {
      const data = await api.post<OtpRequestResponse>('/auth/otp/request', {
        channel: 'phone',
        target: phone,
      });
      setChallengeId(data.challengeId);
      setMessage(data.message);
      setStep('otp');
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { detail?: string } } })?.response?.data?.detail;
      setError(msg ?? 'Failed to send OTP. Please try again.');
    } finally {
      setLoading(false);
    }
  };

  const handleVerifyOtp = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');
    setLoading(true);
    try {
      const data = await api.post<OtpVerifyResponse>('/auth/otp/verify', {
        challengeId,
        otp,
      });
      setToken(data.token);
      navigate('/funds', { replace: true });
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { detail?: string } } })?.response?.data?.detail;
      setError(msg ?? 'Invalid OTP. Please try again.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="min-h-screen flex items-center justify-center bg-gray-50 p-4">
      <div className="max-w-sm w-full bg-white rounded-lg shadow-lg p-4 sm:p-8">
        <div className="flex flex-col items-center mb-6">
          <img src={logo} alt="Fund Management System" className="h-16 w-auto mb-3" />
          <h1 className="text-2xl font-bold text-center text-gray-900">Fund Management System</h1>
        </div>

        {step === 'phone' && (
          <form onSubmit={handleRequestOtp} className="space-y-4">
            <div>
              <label htmlFor="phone" className="block text-sm font-medium text-gray-700 mb-1">
                Phone Number
              </label>
              <input
                id="phone"
                type="tel"
                value={phone}
                onChange={(e) => setPhone(e.target.value)}
                placeholder="+1234567890"
                className="input w-full"
                required
              />
            </div>
            {error && <p className="text-sm text-red-600">{error}</p>}
            <button
              type="submit"
              disabled={loading || !phone.trim()}
              className="w-full py-2 px-4 bg-blue-600 text-white rounded hover:bg-blue-700 disabled:opacity-50 transition-colors"
            >
              {loading ? 'Sending...' : 'Send OTP'}
            </button>
          </form>
        )}

        {step === 'otp' && (
          <form onSubmit={handleVerifyOtp} className="space-y-4">
            {message && <p className="text-sm text-green-600 text-center">{message}</p>}
            <div>
              <label htmlFor="otp" className="block text-sm font-medium text-gray-700 mb-1">
                Enter OTP
              </label>
              <input
                id="otp"
                type="text"
                inputMode="numeric"
                maxLength={6}
                value={otp}
                onChange={(e) => setOtp(e.target.value)}
                placeholder="123456"
                className="input w-full text-center text-lg tracking-widest"
                required
              />
            </div>
            {error && <p className="text-sm text-red-600">{error}</p>}
            <button
              type="submit"
              disabled={loading || otp.length < 4}
              className="w-full py-2 px-4 bg-blue-600 text-white rounded hover:bg-blue-700 disabled:opacity-50 transition-colors"
            >
              {loading ? 'Verifying...' : 'Verify & Login'}
            </button>
            <button
              type="button"
              onClick={() => { setStep('phone'); setOtp(''); setError(''); }}
              className="w-full py-2 px-4 text-gray-600 hover:text-gray-800 text-sm"
            >
              Use a different number
            </button>
          </form>
        )}
      </div>
    </div>
  );
}
