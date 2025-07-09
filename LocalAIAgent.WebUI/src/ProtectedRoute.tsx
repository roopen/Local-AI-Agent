import React from "react";
import type { ReactNode } from "react";
import { Navigate } from "react-router-dom";

interface ProtectedRouteProps {
  condition: boolean;
  redirectTo: string;
  children: ReactNode;
}

const ProtectedRoute: React.FC<ProtectedRouteProps> = ({ condition, redirectTo, children }) => {
  return condition ? <>{children}</> : <Navigate to={redirectTo} replace />;
};

export default ProtectedRoute;
