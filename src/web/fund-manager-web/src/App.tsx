import { Routes, Route, Navigate } from 'react-router-dom';
import ErrorBoundary from '@/components/ErrorBoundary';
import AuthGuard from '@/components/AuthGuard';
import AuthenticatedLayout from '@/components/AuthenticatedLayout';
import LoginPage from '@/pages/auth/LoginPage';
import FundListPage from '@/pages/funds/FundListPage';
import CreateFundPage from '@/pages/funds/CreateFundPage';
import FundDashboardPage from '@/pages/funds/FundDashboardPage';
import MemberListPage from '@/pages/members/MemberListPage';
import ContributionDuesPage from '@/pages/contributions/ContributionDuesPage';
import LedgerPage from '@/pages/ledger/LedgerPage';
import LoanApprovalPage from '@/pages/loans/LoanApprovalPage';
import LoanDetailPage from '@/pages/loans/LoanDetailPage';
import RepaymentSchedulePage from '@/pages/loans/RepaymentSchedulePage';
import VotingSessionPage from '@/pages/loans/VotingSessionPage';
import DissolutionPage from '@/pages/dissolution/DissolutionPage';
import ReportsPage from '@/pages/reports/ReportsPage';
import AuditLogPage from '@/pages/audit/AuditLogPage';
import EntityHistoryPage from '@/pages/audit/EntityHistoryPage';

function App() {
  return (
    <ErrorBoundary>
      <div className="min-h-screen bg-gray-50">
        <Routes>
          <Route path="/" element={<Navigate to="/funds" replace />} />
          <Route path="/login" element={<LoginPage />} />
          <Route path="/funds" element={<AuthGuard><AuthenticatedLayout><FundListPage /></AuthenticatedLayout></AuthGuard>} />
          <Route path="/funds/create" element={<AuthGuard><AuthenticatedLayout><CreateFundPage /></AuthenticatedLayout></AuthGuard>} />
          <Route path="/funds/:fundId" element={<AuthGuard><AuthenticatedLayout><FundDashboardPage /></AuthenticatedLayout></AuthGuard>} />
          <Route path="/funds/:fundId/members" element={<AuthGuard><AuthenticatedLayout><MemberListPage /></AuthenticatedLayout></AuthGuard>} />
          <Route path="/funds/:fundId/contributions" element={<AuthGuard><AuthenticatedLayout><ContributionDuesPage /></AuthenticatedLayout></AuthGuard>} />
          <Route path="/funds/:fundId/ledger" element={<AuthGuard><AuthenticatedLayout><LedgerPage /></AuthenticatedLayout></AuthGuard>} />
          <Route path="/funds/:fundId/loans" element={<AuthGuard><AuthenticatedLayout><LoanApprovalPage /></AuthenticatedLayout></AuthGuard>} />
          <Route path="/funds/:fundId/loans/:loanId" element={<AuthGuard><AuthenticatedLayout><LoanDetailPage /></AuthenticatedLayout></AuthGuard>} />
          <Route path="/funds/:fundId/loans/:loanId/repayments" element={<AuthGuard><AuthenticatedLayout><RepaymentSchedulePage /></AuthenticatedLayout></AuthGuard>} />
          <Route path="/funds/:fundId/loans/:loanId/voting" element={<AuthGuard><AuthenticatedLayout><VotingSessionPage /></AuthenticatedLayout></AuthGuard>} />
          <Route path="/funds/:fundId/dissolution" element={<AuthGuard><AuthenticatedLayout><DissolutionPage /></AuthenticatedLayout></AuthGuard>} />
          <Route path="/funds/:fundId/reports" element={<AuthGuard><AuthenticatedLayout><ReportsPage /></AuthenticatedLayout></AuthGuard>} />
          <Route path="/funds/:fundId/audit" element={<AuthGuard><AuthenticatedLayout><AuditLogPage /></AuthenticatedLayout></AuthGuard>} />
          <Route path="/funds/:fundId/audit/entity-history" element={<AuthGuard><AuthenticatedLayout><EntityHistoryPage /></AuthenticatedLayout></AuthGuard>} />
        </Routes>
      </div>
    </ErrorBoundary>
  );
}

export default App;
