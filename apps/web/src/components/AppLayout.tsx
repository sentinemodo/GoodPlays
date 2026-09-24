import { Outlet } from 'react-router'
import { SiteFooter } from './SiteFooter'
import { SiteNav } from './SiteNav'

export function AppLayout() {
  return (
    <div className="flex min-h-screen flex-col">
      <SiteNav />
      <div className="flex-1">
        <Outlet />
      </div>
      <SiteFooter />
    </div>
  )
}
