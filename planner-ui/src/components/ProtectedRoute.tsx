import { Navigate } from 'react-router-dom';
import { getSession } from '../lib/auth';
import type { ReactNode } from 'react';

export function ProtectedRoute({ children, roles }: { children: ReactNode; roles?: string[] }) {
  const session = getSession();
  if (!session) return <Navigate to="/login" replace />;
  if (roles && !roles.includes(session.roleCode)) return <Navigate to="/" replace />;
  return <>{children}</>;
}
