import { useCallback, useState } from 'react';
import { Link } from 'react-router-dom';
import Badge from '../../../components/Badge';
import PageHeader from '../../../components/PageHeader';
import Pagination from '../../../components/Pagination';
import { EmptyState, ErrorState, LoadingState } from '../../../components/StatusViews';
import { useAuth } from '../../../contexts/AuthContext';
import { useAsync } from '../../../hooks/useAsync';
import { useListQuery } from '../../../hooks/useListQuery';
import { getCampaigns } from '../../../services/campaignService';
import {
  CAMPAIGN_SORT_OPTIONS,
  CAMPAIGN_STATUS_OPTIONS,
  PAGE_SIZE,
} from '../marketingConstants';
import { campaignStatusLabel, campaignStatusTone, formatDate } from '../marketingUtils';

const DEFAULT_QUERY = {
  search: '',
  status: '',
  sortBy: 'startDate',
  sortDirection: 'desc',
  page: 1,
};

export default function CampaignListPage() {
  const { token } = useAuth();
  const [query, setQuery] = useListQuery(DEFAULT_QUERY);
  const [searchText, setSearchText] = useState(query.search);

  const loader = useCallback(
    () => getCampaigns(token, { ...query, pageSize: PAGE_SIZE }),
    [token, query]
  );
  const { data, error, loading, reload } = useAsync(loader);

  const hasFilters = Boolean(query.search || query.status);

  return (
    <>
      <PageHeader
        title="Campaigns"
        actions={<Link className="button" to="/marketing/campaigns/new">New campaign</Link>}
      />

      <form
        className="filters"
        role="search"
        aria-label="Filter campaigns"
        onSubmit={(event) => {
          event.preventDefault();
          setQuery({ search: searchText.trim() });
        }}
      >
        <div className="field">
          <label htmlFor="campaign-search">Search by name</label>
          <div className="input-with-button">
            <input
              id="campaign-search"
              type="search"
              value={searchText}
              onChange={(e) => setSearchText(e.target.value)}
            />
            <button type="submit" className="button">Search</button>
          </div>
        </div>

        <div className="field">
          <label htmlFor="filter-status">Status</label>
          <select id="filter-status" value={query.status} onChange={(e) => setQuery({ status: e.target.value })}>
            <option value="">All statuses</option>
            {CAMPAIGN_STATUS_OPTIONS.map((option) => (
              <option key={option.value} value={option.value}>{option.label}</option>
            ))}
          </select>
        </div>

        <div className="field">
          <label htmlFor="sort-by">Sort by</label>
          <select id="sort-by" value={query.sortBy} onChange={(e) => setQuery({ sortBy: e.target.value })}>
            {CAMPAIGN_SORT_OPTIONS.map((option) => (
              <option key={option.value} value={option.value}>{option.label}</option>
            ))}
          </select>
        </div>

        <div className="field">
          <label htmlFor="sort-direction">Order</label>
          <select
            id="sort-direction"
            value={query.sortDirection}
            onChange={(e) => setQuery({ sortDirection: e.target.value })}
          >
            <option value="desc">Descending</option>
            <option value="asc">Ascending</option>
          </select>
        </div>
      </form>

      {loading && <LoadingState label="Loading campaigns…" />}
      {error && <ErrorState error={error} onRetry={reload} />}

      {!loading && !error && data?.items.length === 0 && (
        <EmptyState
          title={hasFilters ? 'No campaigns match these filters.' : 'No campaigns yet.'}
          action={
            hasFilters ? (
              <button
                type="button"
                className="button button-secondary"
                onClick={() => {
                  setSearchText('');
                  setQuery({ search: '', status: '' });
                }}
              >
                Clear filters
              </button>
            ) : (
              <Link className="button" to="/marketing/campaigns/new">Create a campaign</Link>
            )
          }
        />
      )}

      {!loading && !error && data?.items.length > 0 && (
        <>
          <div className="table-wrap">
            <table>
              <caption className="visually-hidden">Campaigns</caption>
              <thead>
                <tr>
                  <th scope="col">Name</th>
                  <th scope="col">Dates</th>
                  <th scope="col">Status</th>
                  <th scope="col">Promotions</th>
                  <th scope="col"><span className="visually-hidden">Actions</span></th>
                </tr>
              </thead>
              <tbody>
                {data.items.map((campaign) => (
                  <tr key={campaign.id}>
                    <td>
                      <Link to={`/marketing/campaigns/${campaign.id}`}>{campaign.name}</Link>
                    </td>
                    <td>{formatDate(campaign.startDate)} – {formatDate(campaign.endDate)}</td>
                    <td>
                      <Badge tone={campaignStatusTone(campaign.status)}>
                        {campaignStatusLabel(campaign.status)}
                      </Badge>
                    </td>
                    <td>{campaign.promotionCount}</td>
                    <td>
                      <Link
                        className="button button-secondary button-small"
                        to={`/marketing/campaigns/${campaign.id}/edit`}
                        aria-label={`Edit ${campaign.name}`}
                      >
                        Edit
                      </Link>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <Pagination
            page={data.page}
            pageSize={data.pageSize}
            totalCount={data.totalItems}
            onPageChange={(page) => setQuery({ page })}
          />
        </>
      )}
    </>
  );
}
