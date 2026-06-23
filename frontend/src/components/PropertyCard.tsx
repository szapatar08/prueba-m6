import { Link } from 'react-router-dom';
import type { Property } from '../types';

interface PropertyCardProps {
  property: Property;
  isInWishlist?: boolean;
  onToggleWishlist?: (propertyId: string) => void;
  showWishlistButton?: boolean;
}

export default function PropertyCard({
  property,
  isInWishlist = false,
  onToggleWishlist,
  showWishlistButton = false,
}: PropertyCardProps) {
  return (
    <div className="bg-white rounded-lg shadow-md overflow-hidden hover:shadow-lg transition-shadow">
      <Link to={`/properties/${property.id}`}>
        <div className="h-48 bg-gray-200 flex items-center justify-center">
          {property.primaryImageUrl ? (
            <img
              src={property.primaryImageUrl}
              alt={property.name}
              className="w-full h-full object-cover"
            />
          ) : (
            <svg className="w-12 h-12 text-gray-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1} d="M19 21V5a2 2 0 00-2-2H7a2 2 0 00-2 2v16m14 0h2m-2 0h-5m-9 0H3m2 0h5M9 7h1m-1 4h1m4-4h1m-1 4h1m-5 10v-5a1 1 0 011-1h2a1 1 0 011 1v5m-4 0h4" />
            </svg>
          )}
        </div>
      </Link>

      <div className="p-4">
        <div className="flex justify-between items-start">
          <Link to={`/properties/${property.id}`}>
            <h3 className="text-lg font-semibold text-gray-900 hover:text-blue-600">
              {property.name}
            </h3>
          </Link>
          {showWishlistButton && onToggleWishlist && (
            <button
              onClick={(e) => {
                e.preventDefault();
                onToggleWishlist(property.id);
              }}
              className="p-1 rounded-full hover:bg-gray-100"
              aria-label={isInWishlist ? 'Remove from wishlist' : 'Add to wishlist'}
            >
              <svg
                className={`w-6 h-6 ${isInWishlist ? 'text-red-500 fill-current' : 'text-gray-400'}`}
                fill={isInWishlist ? 'currentColor' : 'none'}
                viewBox="0 0 24 24"
                stroke="currentColor"
              >
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4.318 6.318a4.5 4.5 0 000 6.364L12 20.364l7.682-7.682a4.5 4.5 0 00-6.364-6.364L12 7.636l-1.318-1.318a4.5 4.5 0 00-6.364 0z" />
              </svg>
            </button>
          )}
        </div>

        <p className="mt-1 text-sm text-gray-500">
          {property.city}, {property.country}
        </p>

        <div className="mt-3 flex items-center text-sm text-gray-600 space-x-3">
          <span>{property.bedrooms} bed{property.bedrooms !== 1 ? 's' : ''}</span>
          <span>&middot;</span>
          <span>{property.bathrooms} bath{property.bathrooms !== 1 ? 's' : ''}</span>
          <span>&middot;</span>
          <span>{property.maxGuests} guest{property.maxGuests !== 1 ? 's' : ''}</span>
        </div>

        <div className="mt-3 flex items-baseline justify-between">
          <p className="text-lg font-bold text-gray-900">
            ${property.pricePerNight}
            <span className="text-sm font-normal text-gray-500"> / night</span>
          </p>
        </div>
      </div>
    </div>
  );
}
