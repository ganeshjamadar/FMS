import AppHeader from '@/components/AppHeader';

interface AuthenticatedLayoutProps {
  children: React.ReactNode;
}

export default function AuthenticatedLayout({ children }: AuthenticatedLayoutProps) {
  return (
    <>
      <AppHeader />
      <main>{children}</main>
    </>
  );
}
