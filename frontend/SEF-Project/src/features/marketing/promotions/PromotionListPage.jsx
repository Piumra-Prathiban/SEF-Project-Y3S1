import { useCallback, useState } from 'react';
import { Link } from 'react-router-dom';
import Badge from '../../../components/Badge';
import PageHeader from '../../../components/PageHeader';
import Pagination from '../../../components/Pagination';
import { Alert, EmptyState, ErrorState, LoadingState } from '../../../components/StatusViews';
import { useAuth } from '../../../contexts/AuthContext';
import { useAsync } from '../../../hooks/useAsync';
import { useListQuery } from '../../../hooks/useListQuery';
import { getCampaigns } from '../../../services/campaignService';
import { getPromotions, updatePromotion } from '../../../services/promotionService';
import { toErrorMessage } from '../../../utils/apiErrors';
import {
  PAGE_SIZE,
  PROMOTION_SORT_OPTIONS,
  PROMOTION_TYPE_OPTIONS,
} from '../marketingConstants';
import {
  formatDate,
  formatDiscount,
  promotionState,
  withActiveState,
} from '../marketingUtils';

const DEFAULT_QUERY = {
  search: '',
  type: '',
  isActive: '',
  campaignId: '',
  sortBy: 'startDate',
  sortDirection: 'desc',
  page: 1,
};

export default function PromotionListPage() {
  const { token } = useAuth();
  const [query, setQuery] = useListQuery(DEFAULT_QUERY);
  const [searchText, setSearchText] = useState(query.search);
  const [busyId, setBusyId] = useState(null);
  const [message, setMessage] = useState({ tone: 'success', text: '' });

  const loader = useCallback(
    () => getPromotions(token, { ...query, pageSize: PAGE_SIZE }),
    [token, query]
  );
  const { data, error, loading, reload } = useAsync(loader);

  const campaignLoader = useCallback(
    () => getCampaigns(token, { pageSize: 100, sortBy: 'name', sortDirection: 'asc' }),
    [token]
  );
  const campaigns = useAsync(campaignLoader).data?.items ?? [];

  async function handleToggleActive(promotion) {
    setBusyId(promotion.id);
    setMessage({ tone: 'success', text: '' });

    try {
      await updatePromotion(token, promotion.id, withActiveState(promotion, !promotion.isActive));
      setMessage({
        tone: 'success',
        text: `"${promotion.name}" ${promotion.isActive ? 'deactivated' : 'activated'}.`,
      });
      reload();
    } catch (err) {
      setMessage({ tone: 'danger', text: toErrorMessage(err) });
    } finally {
      setBusyId(null);
    }
  }

  const hasFilters = Boolean(query.search || query.type || query.isActive || query.campaignId);

  return (
    <>
      <PageHeader
        title="Promotions"
        actions={<Link className="button" to="/marketing/promotions/new">New promotion</Link>}
      />

      <form
        className="filters"
        role="search"
        aria-label="Filter promotions"
        onSubmit={(event) => {
          event.preventDefault();
          setQuery({ search: searchText.trim() });
        }}
      >
        <div className="field">
          <label htmlFor="promotion-search">Search by name</label>
          <div className="input-with-button">
            <input
              id="promotion-search"
              type="search"
              value={searchText}
              onChange={(e) => setSearchText(e.target.value)}
            />
            <button type="submit" className="button">Search</button>
          </div>
        </div>

        <FilterSelect
          id="filter-type"
          label="Type"
          value={query.type}
          onChange={(type) => setQuery({ type })}
          options={PROMOTION_TYPE_OPTIONS}
          allLabel="All types"
        />
        <FilterSelect
          id="filter-active"
          label="Active"
          value={query.isActive}
          onChange={(isActive) => setQuery({ isActive })}
          options={[
            { value: 'true', label: 'Active' },
            { value: 'false', label: 'Inactive' },
          ]}
          allLabel="All"
        />
        <FilterSelect
          id="filter-campaign"
          label="Campaign"
          value={query.campaignId}
          onChange={(campaignId) => setQuery({ campaignId })}
          options={campaigns.map((c) => ({ value: c.id, label: c.name }))}
          allLabel="All campaigns"
        />
        <FilterSelect
          id="sort-by"
          label="Sort by"
          value={query.sortBy}
          onChange={(sortBy) => setQuery({ sortBy })}
          options={PROMOTION_SORT_OPTIONS}
        />
        <FilterSelect
          id="sort-direction"
          label="Order"
          value={query.sortDirection}
          onChange={(sortDirection) => setQuery({ sortDirection })}
          options={[
            { value: 'desc', label: 'Descending' },
            { value: 'asc', label: 'Ascending' },
          ]}
        />
      </form>

      <Alert tone={message.tone} onDismiss={() => setMessage({ tone: 'success', text: '' })}>
        {message.text}
      </Alert>

      {loading && <LoadingState label="Loading promotions…" />}
      {error && <ErrorState error={error} onRetry={reload} />}

      {!loading && !error && data?.items.length === 0 && (
        <EmptyState
          title={hasFilters ? 'No promotions match these filters.' : 'No promotions yet.'}
          action={
            hasFilters ? (
              <button
                type="button"
                className="button button-secondary"
                onClick={() => {
                  setSearchText('');
                  setQuery({ search: '', type: '', isActive: '', campaignId: '' });
                }}
              >
                Clear filters
              </button>
            ) : (
              <Link className="button" to="/marketing/promotions/new">Create a promotion</Link>
            )
          }
        />
      )}

      {!loading && !error && data?.items.length > 0 && (
        <>
          <div className="table-wrap">
            <table>
              <caption className="visually-hidden">Promotions</caption>
              <thead>
                <tr>
                  <th scope="col">Name</th>
                  <th scope="col">Discount</th>
                  <th scope="col">Campaign</th>
                  <th scope="col">Dates</th>
                  <th scope="col">Status</th>
                  <th scope="col"><span className="visually-hidden">Actions</span></th>
                </tr>
              </thead>
              <tbody>
                {data.items.map((promotion) => {
                  const state = promotionState(promotion);

                  return (
                    <tr key={promotion.id}>
                      <td>
                        <Link to={`/marketing/promotions/${promotion.id}`}>{promotion.name}</Link>
                      </td>
                      <td>{formatDiscount(promotion)}</td>
                      <td>{promotion.campaignName ?? '—'}</td>
                      <td>
                        {formatDate(promotion.startDate)} – {formatDate(promotion.endDate)}
                      </td>
                      <td><Badge tone={state.tone}>{state.label}</Badge></td>
                      <td>
                        <div className="button-row">
                          <Link
                            className="button button-secondary button-small"
                            to={`/marketing/promotions/${promotion.id}/edit`}
                            aria-label={`Edit ${promotion.name}`}
                          >
                            Edit
                          </Link>
                          <button
                            type="button"
                            className="button button-secondary button-small"
                            onClick={() => handleToggleActive(promotion)}
                            disabled={busyId === promotion.id}
                            aria-label={`${promotion.isActive ? 'Deactivate' : 'Activate'} ${promotion.name}`}
                          >
                            {promotion.isActive ? 'Deactivate' : 'Activate'}
                          </button>
                        </div>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
          <Pagination
            page={data.page}
            pageSize={data.pageSize}
            totalCount={data.totalCount}
            onPageChange={(page) => setQuery({ page })}
          />
        </>
      )}
    </>
  );
}

function FilterSelect({ id, label, value, onChange, options, allLabel }) {
  return (
    <div className="field">
      <label htmlFor={id}>{label}</label>
      <select id={id} value={value} onChange={(e) => onChange(e.target.value)}>
        {allLabel && <option value="">{allLabel}</option>}
        {options.map((option) => (
          <option key={option.value} value={option.value}>{option.label}</option>
        ))}
      </select>
    </div>
  );
}
