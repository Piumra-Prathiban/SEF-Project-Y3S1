import { NavLink, Outlet } from 'react-router-dom';
import FlashMessage from '../../components/FlashMessage';

export default function MarketingLayout() {
  return (
    <div className="section-layout">
      <nav className="sub-nav" aria-label="Marketing">
        <NavLink to="/marketing" end>Overview</NavLink>
        <NavLink to="/marketing/promotions">Promotions</NavLink>
        <NavLink to="/marketing/campaigns">Campaigns</NavLink>
      </nav>
      <FlashMessage />
      <Outlet />
    </div>
  );
}
