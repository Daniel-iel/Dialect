UPDATE o
SET o.Status = 'Closed'
FROM [Orders] o
JOIN [Customers] c ON o.CustomerId = c.Id
WHERE c.Region = 'NA';