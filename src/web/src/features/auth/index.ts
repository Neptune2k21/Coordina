// The auth feature as the rest of the app sees it. AuthSection is not here:
// App.tsx loads it lazily, so it stays in its own chunk.
export { AuthProvider, useAuth } from "@/features/auth/auth-context"
