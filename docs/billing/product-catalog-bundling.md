# Product Catalog & Bundling Management (issue #401)

Status of the three v1 capabilities delivered for #401, plus the explicit scope decisions.

## 1. Hierarchical catalog categories

- Entity: `ProductCategory` (`apps/api/Source/Modules/GameGuild.Commerce.Products/Entities/ProductCategory.cs`) — single-parent tree via `ParentCategoryId`, tenant-unique `Slug`, optional `AttributesJson` (jsonb, ≤ 20 keys), `DisplayOrder`.
- Invariants: `ProductCategoryValidator` rejects self-parenting, cycles and trees deeper than `ProductCategory.MaxDepth` (5) **before any write**; the validator works on an id→parent map, so it needs no recursive SQL.
- CRUD: `ProductCategoriesController` (`/v1.0/product-categories`). Reads are public storefront data (registered in `AnonymousEndpointRegistry`); every mutation requires `products:manage`, updates are optimistic-concurrency guarded (`ExpectedVersion`) and audited through `Compliance.Audit` (`ProductCatalogAuditLogger`). Deletes are rejected while child categories or assigned products exist.
- Product assignment: single category per product (`Product.CategoryId`), set through the existing create/update/patch product commands (`categoryId` field) and validated against the tenant catalog (`ProductCategoryAssignment`).
- Catalog filtering: `GET /v1.0/products?categoryId=…` returns products in the category **and all descendant categories** (subtree resolved by `ProductCategoryRepository.GetCategoryAndDescendantIdsAsync`, applied as one translatable IN filter).

## 2. Inventory integration & real-time availability

- Entity: `ProductInventory` (`Entities/ProductInventory.cs`) — `StockQuantity`, `ReservedQuantity`, `TrackInventory` (default **false** = unlimited, preserving the existing digital-goods behavior), `LowStockThreshold`; optimistic concurrency via the standard entity `Version`.
- Order-flow hooks (all additive; untracked products are no-ops):
  - Add-to-order (`AddProductToOrderCommandHandler`): availability check + reservation; fail-closed with `Orders.OutOfStock` on insufficient stock.
  - Complete (`CompleteOrderCommandHandler`): reservation committed to a definitive stock decrement inside the fulfillment transaction; rollback + `Orders.OutOfStockAtFulfillment` if stock shrank meanwhile.
  - Cancel (`CancelOrderCommandHandler`): reservations released.
  - Refund (`RefundOrderCommandHandler`): stock restored on full refunds.
- Admin: `PUT /v1.0/products/{id}/inventory` (`products:manage`, audited) creates/reconfigures the inventory row, preserving outstanding reservations.
- Real-time availability: `GET /v1.0/products/{id}/availability` (public) reports `trackInventory`, on-hand, reserved, available, in-stock and low-stock flags. Untracked products report always-in-stock.
- Catalog filtering: `GET /v1.0/products?inStock=true|false` filters by the same availability rule.

## 3. Bundle optimization & recommendations (v1: rule-based)

- Endpoint: `GET /v1.0/products/{id}/recommendations` (`ProductRecommendationsController`, Orders module — co-occurrence reads order history). Deterministic rules, no ML:
  - **Frequently bought together** — products sharing completed (`Completed`/`Fulfilled`) orders with the anchor, ranked by co-order count, published products only, up to 500 historic orders scanned, cached 5 minutes in the sized platform memory cache.
  - **Bundle suggestions with savings hint** — published bundles containing the anchor; `BundleSavingsCalculator` compares the bundle's effective default price against the sum of component effective default prices (after per-item `BundleDiscountPercentage`), yielding savings amount/percentage.
  - **Complete the bundle** — optional (`IsRequired = false`) components of the anchor when it is itself a bundle.

### Deferred: machine-learning recommendations

ML-based personalization (embedding/collaborative-filtering models, per-user ranking,
A/B-tested re-ranking) is intentionally **out of scope for v1** and deferred. The v1
surface is deterministic and auditable (pure SQL aggregation + pure functions), which the
acceptance criteria accept as "optimization and recommendations" groundwork. Reopen
criteria for ML: a capacity model for training/serving, a feature store decision, and a
product decision on personalization vs. global ranking.
