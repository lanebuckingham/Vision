"use client";

import { useEffect } from "react";
import { useRouter } from "next/navigation";
import { useAuth } from "@/lib/auth/AuthContext";

/**
 * OAuth callback page. The AuthProvider handles the authorization code exchange
 * on mount (it reads ?code= and ?state= from the URL). This page observes
 * auth state and navigates when authentication completes.
 */
export default function AuthCallbackPage() {
  const router = useRouter();
  const { isAuthenticated, isLoading } = useAuth();

  useEffect(() => {
    if (!isLoading && isAuthenticated) {
      router.replace("/dashboard");
    }
  }, [isLoading, isAuthenticated, router]);

  return (
    <div className="flex h-[50vh] items-center justify-center">
      <div className="text-center">
        <div className="h-8 w-8 animate-spin rounded-full border-4 border-gray-300 border-t-blue-600 mx-auto" />
        <p className="mt-3 text-sm text-gray-500 dark:text-gray-400">Completing sign in...</p>
      </div>
    </div>
  );
}
