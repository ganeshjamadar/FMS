import { useNavigate, Link } from 'react-router-dom';
import { useAuthStore } from '@/stores/authStore';
import logo from '@/images/logo/Web-Logo.png';

export default function AppHeader() {
  const navigate = useNavigate();
  const { logout, isLoggingOut } = useAuthStore();

  const handleLogout = async () => {
    await logout();
    navigate('/login', { replace: true });
  };

  return (
    <header className="bg-white border-b border-gray-200 shadow-sm">
      <div className="w-full px-4 sm:px-6 lg:px-8 xl:px-16 2xl:px-24 h-14 flex items-center justify-between">
        <Link to="/funds" className="flex items-center gap-2 text-base sm:text-lg font-bold text-blue-700 hover:text-blue-800 transition-colors">
          <img src={logo} alt="Fund Management System" className="h-8 w-auto" />
          <span className="hidden sm:inline">Fund Management System</span>
          <span className="sm:hidden">FMS</span>
        </Link>

        <button
          onClick={handleLogout}
          disabled={isLoggingOut}
          className="inline-flex items-center gap-1.5 px-3 py-1.5 text-sm font-medium text-gray-700 bg-gray-100 rounded-lg hover:bg-gray-200 hover:text-red-600 transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
        >
          {isLoggingOut ? (
            <>
              <svg className="animate-spin h-4 w-4" viewBox="0 0 24 24" fill="none">
                <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
                <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" />
              </svg>
              Signing out…
            </>
          ) : (
            <>
              <svg xmlns="http://www.w3.org/2000/svg" className="h-4 w-4" viewBox="0 0 20 20" fill="currentColor">
                <path fillRule="evenodd" d="M3 3a1 1 0 00-1 1v12a1 1 0 001 1h12a1 1 0 001-1V4a1 1 0 00-1-1H3zm10.293 9.293a1 1 0 001.414 1.414l3-3a1 1 0 000-1.414l-3-3a1 1 0 10-1.414 1.414L14.586 9H7a1 1 0 100 2h7.586l-1.293 1.293z" clipRule="evenodd" />
              </svg>
              Logout
            </>
          )}
        </button>
      </div>
    </header>
  );
}
