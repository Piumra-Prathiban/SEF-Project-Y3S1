import { NavLink, Outlet } from 'react-router-dom';
import FlashMessage from '../../components/FlashMessage';

export default function MarketingLayout() {
  return (
    <div className="section-layout">
      <nav className="sub-nav" aria-label="Marketing">
        <NavLink to="/marketing" end>Overview</NavLink>
        <NavLink to="/marketing/dashboard">Dashboard</NavLink>
        <NavLink to="/marketing/analytics">Analytics</NavLink>
        <NavLink to="/marketing/reports">Reports</NavLink>
        <NavLink to="/marketing/promotions">Promotions</NavLink>
        <NavLink to="/marketing/campaigns">Campaigns</NavLink>
        <NavLink to="/marketing/agent">Promotion Agent</NavLink>
      </nav>
      <FlashMessage />
      <Outlet />
    </div>
  );
}
