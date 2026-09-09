UPDATE "Orders" o
SET "Status" = 'Closed'
FROM "Customers" c
WHERE o."CustomerId" = c."Id" AND c."Region" = 'NA';