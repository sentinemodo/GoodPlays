import { useAuth } from '@clerk/clerk-react'
import { useEffect } from 'react'
import { setApiTokenProvider } from '../lib/api'

export function useApiAuth() {
  const { getToken, isLoaded } = useAuth()

  useEffect(() => {
    if (!isLoaded) {
      return
    }

    setApiTokenProvider(async () => getToken())
  }, [getToken, isLoaded])
}
