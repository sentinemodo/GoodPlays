import { ClerkProvider } from '@clerk/clerk-react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { BrowserRouter, Route, Routes } from 'react-router'
import { HomePage } from './pages/HomePage'
import { LibraryPage } from './pages/LibraryPage'
import { PrivacyPage } from './pages/PrivacyPage'
import { SignInPage } from './pages/SignInPage'

const queryClient = new QueryClient()
const publishableKey = import.meta.env.VITE_CLERK_PUBLISHABLE_KEY
const routerBasename = import.meta.env.BASE_URL.replace(/\/$/, '')

function AppRoutes() {
  return (
    <BrowserRouter basename={routerBasename || undefined}>
      <Routes>
        <Route path="/" element={<HomePage />} />
        <Route path="/library" element={<LibraryPage />} />
        <Route path="/privacy" element={<PrivacyPage />} />
        <Route path="/sign-in/*" element={<SignInPage />} />
      </Routes>
    </BrowserRouter>
  )
}

export default function App() {
  if (!publishableKey) {
    return (
      <QueryClientProvider client={queryClient}>
        <div className="p-6 text-amber-300">
          Set <code className="text-emerald-300">VITE_CLERK_PUBLISHABLE_KEY</code>{' '}
          {import.meta.env.PROD ? (
            <>
              as a GitHub Actions secret and redeploy Pages (see README).
            </>
          ) : (
            <>
              in <code className="text-emerald-300">apps/web/.env</code> to enable Clerk auth.
            </>
          )}
        </div>
        <AppRoutes />
      </QueryClientProvider>
    )
  }

  return (
    <ClerkProvider publishableKey={publishableKey}>
      <QueryClientProvider client={queryClient}>
        <AppRoutes />
      </QueryClientProvider>
    </ClerkProvider>
  )
}
