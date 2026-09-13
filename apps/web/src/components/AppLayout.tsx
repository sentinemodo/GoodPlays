import { Outlet } from 'react-router'
import { SiteFooter } from './SiteFooter'

export function AppLayout() {
  return (
    <div className="flex min-h-screen flex-col">
      <div className="flex-1">
        <Outlet />
      </div>
      <SiteFooter />
    </div>
  )
}
